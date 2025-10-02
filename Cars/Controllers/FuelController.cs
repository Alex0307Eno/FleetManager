using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/fuel")]
    public class FuelController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public FuelController(ApplicationDbContext db) => _db = db;

        // —— 共用：卡號正規化（去 BOM/空白/破折號；可按需加全形→半形） ——
        private static string Norm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim().TrimStart('\ufeff').Replace('\u00A0', ' ');
            s = s.Replace(" ", "").Replace("-", "").Replace("—", "").Replace("–", "");
            return s;
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportCpcCsv([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("請上傳 CSV 檔");

            // === 讀檔（先 BOM，再 UTF8，最後 Big5） ===
            string text;
            await using (var s = file.OpenReadStream())
            {
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms);
                var bytes = ms.ToArray();

                bool hasUtf8Bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                if (hasUtf8Bom)
                {
                    text = System.Text.Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    var utf8 = System.Text.Encoding.UTF8.GetString(bytes);
                    int badCount = utf8.Count(c => c == '�' || c == '?');
                    text = (badCount > Math.Max(10, utf8.Length / 50))
                        ? System.Text.Encoding.GetEncoding(950).GetString(bytes) // Big5
                        : utf8;
                }
            }

            var rows = ParseCpcCsv(text);

            // === 建字典：NormalizedCardNo → FuelCardId（不再用 VehicleId 對應；VehicleId 轉由 JOIN 取得） ===
            var cardIdMap = (await _db.Set<FuelCard>()
                    .Select(x => new { x.FuelCardId, x.CardNo })
                    .ToListAsync())
                .GroupBy(x => Norm(x.CardNo))
                .ToDictionary(g => g.Key, g => g.First().FuelCardId);

            var before = await _db.Set<FuelTransaction>().CountAsync();

            int ok = 0, dup = 0, bad = 0;
            var unmatchedCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rows)
            {
                // 去重 hash：卡號 + 時間(到分) + 金額(四捨五入到元)
                var key = $"{r.CardNo}|{r.TxTime:yyyy-MM-dd HH:mm}|{Math.Round(r.Amount, 0)}";
                var hash = Convert.ToBase64String(
                    System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));

                if (await _db.Set<FuelTransaction>().AnyAsync(x => x.RawHash == hash))
                { dup++; continue; }

                // 找 FuelCardId（必要）
                int? fuelCardId = null;
                var norm = Norm(r.CardNo);
                if (!string.IsNullOrEmpty(norm) && cardIdMap.TryGetValue(norm, out var cid))
                    fuelCardId = cid;

                if (fuelCardId is null)
                {
                    bad++;
                    unmatchedCards.Add(r.CardNo ?? "");
                    continue; // 嚴格：對不到卡片就跳過，不寫入
                }

                var tx = new FuelTransaction
                {
                    TxTime = r.TxTime,
                    FuelCardId = fuelCardId.Value,  // ✅ 關鍵：用外鍵串卡片
                    StationName = r.Station,
                    CardNo = r.CardNo,              // 原始卡號保留稽核
                    PlateNo = r.PlateNo ?? "",      // 若 DB NOT NULL → 給空字串也可
                    Liters = r.Liters,
                    UnitPrice = r.UnitPrice,
                    Amount = r.Amount,
                    Odometer = r.Odometer,
                    RawHash = hash,
                    SourceFileName = file.FileName,
                    ImportedAt = DateTime.Now
                };

                try
                {
                    _db.Set<FuelTransaction>().Add(tx);
                    ok++;
                }
                catch
                {
                    bad++;
                }
            }

            var affected = await _db.SaveChangesAsync();
            var after = await _db.Set<FuelTransaction>().CountAsync();

            var cx = _db.Database.GetDbConnection();

            return Ok(new
            {
                imported = ok,
                duplicated = dup,
                failed = bad,
                unmatchedCardCount = unmatchedCards.Count,
                unmatchedCards = unmatchedCards.Take(50),
                saveChangesAffected = affected,
                before,
                after,
                db = new
                {
                    provider = _db.Database.ProviderName,
                    dataSource = cx.DataSource,
                    database = cx.Database
                }
            });
        }

        // —— CSV 解析（保留你已完成的健壯性） ——
        private static List<CpcRow> ParseCpcCsv(string csv)
        {
            var list = new List<CpcRow>();
            using var reader = new StringReader(csv);

            var header = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(header))
                throw new Exception("CSV 檔案缺少標頭列");

            string[] hTab = header.Split('\t');
            string[] hComma = header.Split(',');
            char primary = hTab.Length > hComma.Length ? '\t' : ',';
            char secondary = primary == '\t' ? ',' : '\t';

            var idx = DetectHeaderIndex(header, primary);

            var requiredNames = new[] { "交易日期/日期", "交易時間/時間", "卡號/燃料卡號", "公升數/數量", "單價", "金額/總額" };
            var requiredIdxs = new[] { idx.Date, idx.Time, idx.CardNo, idx.Liters, idx.UnitPrice, idx.Amount };
            for (int i = 0; i < requiredIdxs.Length; i++)
                if (requiredIdxs[i] < 0)
                    throw new Exception($"CSV 標頭缺少必要欄位：{requiredNames[i]}（請確認標頭文字與分隔符）");

            string? line;
            int lineNo = 1;

            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = SplitRow(line, primary);

                int requiredMax = new[] { idx.Date, idx.Time, idx.CardNo, idx.Liters, idx.UnitPrice, idx.Amount, Math.Max(idx.Station, Math.Max(idx.PlateNo, idx.Odometer)) }.Max();
                if (cols.Length <= requiredMax && line.IndexOf(secondary) >= 0)
                {
                    var alt = SplitRow(line, secondary);
                    if (alt.Length > cols.Length) cols = alt;
                }

                try
                {
                    if (cols.Length <= requiredMax)
                    {
                        Console.WriteLine($"[ERROR] 第 {lineNo} 列欄位不足（{cols.Length} < {requiredMax + 1}）：{line}");
                        continue;
                    }

                    if (!DateTime.TryParse($"{cols[idx.Date]} {cols[idx.Time]}", out var txTime))
                    {
                        Console.WriteLine($"[WARN] 第 {lineNo} 列日期時間解析失敗：{cols[idx.Date]} {cols[idx.Time]}");
                        continue;
                    }

                    decimal TryDec(string s) { decimal.TryParse((s ?? "").Replace(",", ""), out var v); return v; }

                    var liters = TryDec(cols[idx.Liters]);
                    var unitPrice = TryDec(cols[idx.UnitPrice]);
                    var amount = TryDec(cols[idx.Amount]);

                    int? odo = null;
                    if (idx.Odometer >= 0)
                    {
                        var raw = (cols[idx.Odometer] ?? "").Replace(",", "");
                        if (int.TryParse(raw, out var tmp)) odo = tmp;
                    }

                    var station = idx.Station >= 0 ? cols[idx.Station] : null;
                    var plate = idx.PlateNo >= 0 ? cols[idx.PlateNo] : null;

                    list.Add(new CpcRow
                    {
                        TxTime = txTime,
                        Station = station,
                        CardNo = cols[idx.CardNo],
                        PlateNo = plate,
                        Liters = liters,
                        UnitPrice = unitPrice,
                        Amount = amount,
                        Odometer = odo
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 第 {lineNo} 列解析失敗：{ex.Message}\n原始內容：{line}");
                }
            }

            return list;
        }

        private record CpcRow
        {
            public DateTime TxTime { get; init; }
            public string? Station { get; init; }
            public string CardNo { get; init; } = "";
            public string? PlateNo { get; init; }
            public decimal Liters { get; init; }
            public decimal UnitPrice { get; init; }
            public decimal Amount { get; init; }
            public int? Odometer { get; init; }
        }

        private static (int Date, int Time, int Station, int CardNo, int PlateNo, int Liters, int UnitPrice, int Amount, int Odometer)
        DetectHeaderIndex(string header, char delimiter)
        {
            var cols = header.Split(delimiter).Select(s => s.Trim().Trim('"')).ToArray();

            for (int i = 0; i < cols.Length; i++)
                cols[i] = cols[i].TrimStart('\ufeff').Replace('\u00A0', ' ').Trim();

            int idx(string name, params string[] aliases)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (string.Equals(c, name, StringComparison.Ordinal)) return i;
                    if (aliases.Any(a => string.Equals(c, a, StringComparison.Ordinal))) return i;
                }
                return -1;
            }

            return (
                Date: idx("交易日期", "日期"),
                Time: idx("交易時間", "時間"),
                Station: idx("加油站", "站名"),
                CardNo: idx("卡號", "燃料卡號"),
                PlateNo: idx("車號"),
                Liters: idx("公升數", "數量"),
                UnitPrice: idx("單價"),
                Amount: idx("金額", "總額"),
                Odometer: idx("里程數", "里程")
            );
        }

        private static string[] SplitRow(string line, char delimiter)
        {
            if (!line.Contains('"'))
                return line.Split(delimiter).Select(s => s.Trim().Trim('"')).ToArray();

            var list = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    { sb.Append('"'); i++; }
                    else { inQuotes = !inQuotes; }
                }
                else if (ch == delimiter && !inQuotes)
                {
                    list.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
            list.Add(sb.ToString().Trim());
            return list.Select(s => s.Trim('"')).ToArray();
        }

        // === 以 FuelCardId JOIN FuelCards 拿 VehicleId 來統計 ===
        [HttpGet("stats/{year:int}/{month:int}")]
        public async Task<IActionResult> FuelStats(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var rows = await (
                from t in _db.FuelTransaction
                where t.TxTime >= start && t.TxTime < end
                join c in _db.FuelCards on t.FuelCardId equals c.FuelCardId
                join v in _db.Vehicles on c.VehicleId equals v.VehicleId into vj
                from v in vj.DefaultIfEmpty()
                select new
                {
                    c.VehicleId,
                    PlateNo = (v != null && !string.IsNullOrWhiteSpace(v.PlateNo)) ? v.PlateNo : t.PlateNo,
                    t.TxTime,
                    t.Liters,
                    t.Amount,
                    t.Odometer
                }
            ).ToListAsync();

           
             
            var result = rows
                .GroupBy(x => new { x.VehicleId, PlateNo = x.PlateNo ?? "" })
                .Select(g =>
                {
                    var ordered = g.OrderBy(x => x.TxTime).ToArray();
                    decimal liters = ordered.Sum(x => x.Liters);
                    decimal amount = ordered.Sum(x => x.Amount);

                    var odo = ordered.Where(x => x.Odometer.HasValue).ToArray();
                    decimal km = 0m;
                    for (int i = 1; i < odo.Length; i++)
                    {
                        var diff = odo[i].Odometer!.Value - odo[i - 1].Odometer!.Value;
                        if (diff > 0) km += diff;
                    }
                    var kml = (liters > 0 && km > 0) ? (km / liters) : 0m;

                    return new
                    {
                        vehicleId = g.Key.VehicleId,
                        plateNo = g.Key.PlateNo,     // ✅ 回傳車牌
                        totalLiters = Math.Round(liters, 2),
                        totalAmount = amount,
                        totalKm = km,
                        kmPerLiter = Math.Round(kml, 2)
                    };
                })
                .ToList();

            return Ok(result);
        }
        [HttpGet("cards/{year:int}/{month:int}")]
        public async Task<IActionResult> CardStats(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            // 以 FuelCards 為主，左連結當月交易
            var q = from c in _db.FuelCards
                    join v in _db.Vehicles on c.VehicleId equals v.VehicleId into vj
                    from v in vj.DefaultIfEmpty()
                    join t in _db.FuelTransaction.Where(t => t.TxTime >= start && t.TxTime < end)
                         on c.FuelCardId equals t.FuelCardId into tj
                    from t in tj.DefaultIfEmpty()
                    select new
                    {
                        c.FuelCardId,
                        c.CardNo,
                        c.VehicleId,
                        PlateNo = v != null ? v.PlateNo : null,
                        c.IsActive,
                        TxTime = (DateTime?)t.TxTime,
                        Liters = (decimal?)(t != null ? t.Liters : 0m),
                        Amount = (decimal?)(t != null ? t.Amount : 0m)
                    };

            var rows = await q.ToListAsync();

            var result = rows
                .GroupBy(x => new { x.FuelCardId, x.CardNo, x.VehicleId, x.PlateNo, x.IsActive })
                .Select(g =>
                {
                    var txs = g.Where(x => x.TxTime.HasValue).ToArray();
                    var totalLiters = txs.Sum(x => x.Liters ?? 0m);
                    var totalAmount = txs.Sum(x => x.Amount ?? 0m);
                    var lastTx = txs.Any() ? txs.Max(x => x.TxTime) : null;
                    return new
                    {
                        fuelCardId = g.Key.FuelCardId,
                        cardNo = g.Key.CardNo,
                        vehicleId = g.Key.VehicleId,
                        plateNo = g.Key.PlateNo,
                        isActive = g.Key.IsActive,
                        txCount = txs.Length,
                        totalLiters = Math.Round(totalLiters, 2),
                        totalAmount = totalAmount,
                        lastTxTime = lastTx
                    };
                })
                .OrderByDescending(x => x.totalAmount)
                .ToList();

            return Ok(result);
        }


    }
}
