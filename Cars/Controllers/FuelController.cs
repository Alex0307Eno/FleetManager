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

        [HttpPost("import")]
        public async Task<IActionResult> ImportCpcCsv([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("請上傳 CSV 檔");

            // 讀檔（BIG5 失敗就 UTF8）
            string text;
            await using (var s = file.OpenReadStream())
            {
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms);
                var bytes = ms.ToArray();
                try
                {
                    var big5 = System.Text.Encoding.GetEncoding(950);
                    text = big5.GetString(bytes);
                }
                catch
                {
                    text = System.Text.Encoding.UTF8.GetString(bytes);
                }
            }

            var rows = ParseCpcCsv(text);

            // 匯入前紀錄數
            var before = await _db.Set<FuelFillUp>().CountAsync();

            // 卡號→車輛 對應表
            var cardMap = await _db.Set<FuelCard>().ToDictionaryAsync(x => x.CardNo, x => x.VehicleId);

            int ok = 0, dup = 0, bad = 0;
            foreach (var r in rows)
            {
                var key = $"{r.CardNo}|{r.TxTime:yyyy-MM-dd HH:mm}|{Math.Round(r.Amount, 0)}";
                var hash = Convert.ToBase64String(
                    System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));

                if (await _db.Set<FuelFillUp>().AnyAsync(x => x.RawHash == hash)) { dup++; continue; }

                var tx = new FuelFillUp
                {
                    TxTime = r.TxTime,
                    StationName = r.Station,
                    CardNo = r.CardNo,
                    VehicleId = cardMap.TryGetValue(r.CardNo ?? "", out var vid) ? vid : (int?)null,
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
                    _db.Set<FuelFillUp>().Add(tx);
                    ok++;
                }
                catch
                {
                    bad++;
                }
            }

            var affected = await _db.SaveChangesAsync();
            var after = await _db.Set<FuelFillUp>().CountAsync();

            // 印出實際連到的 DB 資訊
            var cx = _db.Database.GetDbConnection();

            return Ok(new
            {
                imported = ok,
                duplicated = dup,
                failed = bad,
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



        // 簡易 CSV 解析器（請依你的檔案標頭調整 mapping）
        private static List<CpcRow> ParseCpcCsv(string csv)
        {
            var list = new List<CpcRow>();
            using var reader = new StringReader(csv);
            string? line;

            // 先讀標頭
            var header = reader.ReadLine();
            var idx = DetectHeaderIndex(header);

            int lineNo = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = SplitCsv(line);

                try
                {
                    DateTime txTime;
                    if (!DateTime.TryParse($"{cols[idx.Date]} {cols[idx.Time]}", out txTime))
                    {
                        Console.WriteLine($"[WARN] 第 {lineNo} 列日期時間解析失敗：{cols[idx.Date]} {cols[idx.Time]}");
                        continue;
                    }

                    decimal liters = 0m;
                    decimal.TryParse(cols[idx.Liters], out liters);

                    decimal unitPrice = 0m;
                    decimal.TryParse(cols[idx.UnitPrice], out unitPrice);

                    decimal amount = 0m;
                    decimal.TryParse(cols[idx.Amount], out amount);

                    int? odo = null;
                    if (idx.Odometer >= 0 && int.TryParse(cols[idx.Odometer], out var tmpOdo))
                        odo = tmpOdo;

                    var row = new CpcRow
                    {
                        TxTime = txTime,
                        Station = cols[idx.Station],
                        CardNo = cols[idx.CardNo],
                        Liters = liters,
                        UnitPrice = unitPrice,
                        Amount = amount,
                        Odometer = odo
                    };

                    Console.WriteLine($"[DEBUG] 匯入列：時間={row.TxTime}, 卡號={row.CardNo}, 公升={row.Liters}, 金額={row.Amount}, 里程={row.Odometer}");
                    list.Add(row);
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
            public string Station { get; init; }
            public string CardNo { get; init; }
            public decimal Liters { get; init; }
            public decimal UnitPrice { get; init; }
            public decimal Amount { get; init; }
            public int? Odometer { get; init; }
        }

        private static (int Date, int Time, int Station, int CardNo,  int Liters, int UnitPrice, int Amount, int Odometer)
    DetectHeaderIndex(string? header)
        {
            if (string.IsNullOrEmpty(header))
                throw new Exception("CSV 檔案缺少標頭列");

            var cols = header.Split(',').Select(s => s.Trim().Trim('"')).ToArray();

            int idx(string name, params string[] aliases)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] == name || aliases.Contains(cols[i])) return i;
                }
                return -1;
            }

            return (
                Date: idx("交易日期", "日期"),
                Time: idx("交易時間", "時間"),
                Station: idx("加油站", "站名"),
                CardNo: idx("卡號", "燃料卡號"),
                Liters: idx("公升數", "數量"),
                UnitPrice: idx("單價"),
                Amount: idx("金額", "總額"),
                Odometer: idx("里程數", "里程")
            );
        }


        private static string[] SplitCsv(string line)
        {
            // 可換成 CSV 套件；這裡用最簡單 split，假設中油檔沒有內嵌逗號
            return line.Split(',').Select(s => s.Trim().Trim('"')).ToArray();
        }

        public async Task<IActionResult> FuelStats(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var q = _db.FuelFillUps
                .Where(x => x.TxTime >= start && x.TxTime < end && x.VehicleId != null)
                .OrderBy(x => x.VehicleId).ThenBy(x => x.TxTime)
                .AsEnumerable() // client 計算相鄰差
                .GroupBy(x => x.VehicleId)
                .Select(g =>
                {
                    decimal liters = g.Sum(x => x.Liters);
                    decimal amount = g.Sum(x => x.Amount);

                    // 相鄰里程差
                    var arr = g.Where(x => x.Odometer.HasValue).OrderBy(x => x.TxTime).ToArray();
                    var km = 0m;
                    for (int i = 1; i < arr.Length; i++)
                    {
                        var diff = arr[i].Odometer!.Value - arr[i - 1].Odometer!.Value;
                        if (diff > 0) km += diff;
                    }
                    var kml = (liters > 0 && km > 0) ? (km / liters) : 0m;

                    return new
                    {
                        vehicleId = g.Key,
                        totalLiters = Math.Round(liters, 2),
                        totalAmount = amount,
                        totalKm = km,
                        kmPerLiter = Math.Round(kml, 2)
                    };
                });

            var list = q.ToList();
            return Ok(list);
        }

    }

}
