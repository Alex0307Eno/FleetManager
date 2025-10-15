using Cars.Shared.Dtos.CarApplications;
using System;


namespace Cars.Shared.Line
{
    public static class MessageTemplates
    {
        public static string BuildManagerReviewBubble(CarApplicationDto a)
        {
            return $@"
{{
  ""type"": ""flex"",
  ""altText"": ""派車申請通知"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""styles"": {{
      ""body"": {{
        ""backgroundColor"": ""#ffffff""
      }}
    }},
    ""body"": {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""spacing"": ""md"",
      ""paddingAll"": ""16px"",
      ""contents"": [
        {{
          ""type"": ""text"",
          ""text"": ""🚗 新派車申請"",
          ""weight"": ""bold"",
          ""size"": ""lg"",
          ""color"": ""#0f172a""
        }},
        {{
          ""type"": ""text"",
          ""text"": ""申請人：{a.ApplicantName ?? "—"} ({a.ApplicantDept ?? "—"})"",
          ""size"": ""sm"",
          ""color"": ""#334155""
        }},
        {{
          ""type"": ""text"",
          ""text"": ""時間：{a.UseStart:MM/dd HH:mm} - {a.UseEnd:HH:mm}"",
          ""size"": ""sm"",
          ""color"": ""#334155"",
          ""wrap"": true
        }},
        {{
          ""type"": ""text"",
          ""text"": ""路線：{(a.Origin ?? "公司")} → {a.Destination ?? "未填寫"}"",
          ""size"": ""sm"",
          ""color"": ""#475569"",
          ""wrap"": true
        }},
        {{
          ""type"": ""text"",
          ""text"": ""乘客：{a.PassengerCount ?? 1} 人｜行程：{(string.Equals(a.TripType, "round", StringComparison.OrdinalIgnoreCase) ? "來回" : "單程")}"",
          ""size"": ""sm"",
          ""color"": ""#475569""
        }},
        {{
          ""type"": ""text"",
          ""text"": ""事由：{a.ApplyReason ?? "—"}"",
          ""size"": ""sm"",
          ""color"": ""#64748b"",
          ""wrap"": true
        }},
        {{
          ""type"": ""separator"",
          ""margin"": ""md""
        }},
        {{
          ""type"": ""box"",
          ""layout"": ""horizontal"",
          ""spacing"": ""md"",
          ""margin"": ""sm"",
          ""contents"": [
            {{
              ""type"": ""button"",
              ""style"": ""secondary"",
              ""color"": ""#ef4444"",
              ""height"": ""sm"",
              ""action"": {{
                ""type"": ""postback"",
                ""label"": ""❌ 駁回"",
                ""data"": ""action=reviewReject&applyId={a.ApplyId}""
              }}
            }},
            {{
              ""type"": ""button"",
              ""style"": ""primary"",
              ""color"": ""#22c55e"",
              ""height"": ""sm"",
              ""action"": {{
                ""type"": ""postback"",
                ""label"": ""✅ 同意"",
                ""data"": ""action=reviewApprove&applyId={a.ApplyId}""
              }}
            }}
          ]
        }}
      ]
    }}
  }}
}}";
        }
    }
}
