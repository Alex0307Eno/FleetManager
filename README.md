# 🚗 FleetManager Demo

FleetManager 是一個以 **實務派車與駕駛管理流程** 為核心的系統，  
展示後端架構、資料建模、通知整合與排程任務自動化的完整實作。  

本版本為個人獨立開發 Demo，用於展示系統設計與實作能力，  
資料皆為模擬內容，與任何商用或機關專案無關。

---

## 🧱 系統架構

**後端技術**
- ASP.NET Core MVC (Clean Architecture)
- Entity Framework Core (Code First, Transaction Safe)
- LINQ / ADO.NET (Hybrid Query)
- Hangfire (排程與背景任務)
- LINE Bot API (通知模組整合)

**前端技術**
- Vue 3 (以漸進式整合方式嵌入 Razor View)
- Axios 通訊模組
- Flatpickr / DataTable 前端組件

**資料庫**
- SQL Server (自動 Migration + 索引優化)
- 完整的關聯設計 (駕駛、派工、申請、假單、代理、審核紀錄、通知)

---

## 🧩 主要功能模組

| 模組 | 說明 |
|------|------|
| 🚙 派工管理 | 建立派車單、指派駕駛與車輛、紀錄派工異動歷程 |
| 👨‍✈️ 駕駛管理 | 支援代理制度與班表設定，可追蹤駕駛出勤狀態 |
| 🕓 請假審核 | 審核駕駛請假單，系統自動判斷受影響行程並提示管理員 |
| ⚙ 通知中心 | 整合 LINE Bot，提供即時派車、審核與提醒訊息推播 |
| 🗓 排程提醒 | Hangfire 定時推播「前一日提醒」與「15 分鐘前提醒」 |
| 📊 系統稽核 | 自動記錄所有派工與狀態異動，支援 JSON 差異比較 |

---

## ⚡ 專案挑戰與解決方案

| 問題 | 解法 |
|------|------|
| **駕駛請假造成派工衝突** | 在核准請假時自動比對派工表，產生受影響清單並提示重新指派 |
| **通知系統無法維護** | 將 LINE Bot 推播封裝為 `NotificationService`，可插拔 SMS、Email 模組 |
| **資料一致性風險高** | 所有變更動作皆封裝於 Service Transaction 中，確保多表同步寫入 |
| **後端排程混亂** | 使用 Hangfire 將提醒與通知任務分層託管，支援延遲與週期任務 |
| **審核缺乏歷史追蹤** | 新增 `Audit` 模組，自動記錄前後差異 JSON，利於問題回溯 |

---

## 💡 系統設計思維

> 「人走、系統不倒；駕駛請假、任務不中斷。」

整體設計以「可自我修復」為核心概念，  
當駕駛請假、任務異動或資料更新時，系統能自動檢查衝突、提示異常並推播通知。  
同時透過模組化設計，使每個子系統（派工、請假、通知、排程）可獨立維運與測試。

---

## 🧠 技術亮點

- Clean Architecture + DDD 分層  
- EF Core Transaction / Concurrency Control  
- Hangfire 自動化任務佇列  
- LINE Bot API 推播與使用者互動  
- SQL 效能調校與索引優化  
- 完整 API 層 + DTO 對應設計  

---

## 💻 開發環境

| 類別 | 工具 |
|------|------|
| IDE | Visual Studio 2022 / VS Code |
| Framework | .NET 8.0 |
| Database | SQL Server 2019 |
| Queue | Hangfire |
| Source Control | Git + GitHub |
| OS | Windows 10 / 11 |

---

## 📜 Career Background

| 時間 | 職位 / 公司 | 工作內容 |
|------|--------------|----------|
| 2025 – Present | **.NET Developer @ 豐醇科技** | 參與政府標案系統開發，負責後端模組邏輯與排程整合 |
| 2021 – 2023 | **技術員 @ 台灣精銳機械** | CNC 自動化與製程優化，建立維修與監控資料流程 |
| 2017 – 2020 | **助理工程師 @ 大立光電** | 光電設備維護與資料追蹤系統改善，導入初步自動化報表 |
| 2024 – 2025 | **資策會 – 網站開發課程** | 專攻 ASP.NET Core MVC、Entity Framework、前後端整合技術 |

> 多年的設備背景讓我在軟體開發中特別重視「資料一致性」與「可維護性」，  
> 並透過這個專案展示如何將企業流程轉化為可擴充的系統架構。

---

## 👨‍💻 作者

**Alex**  
Full-Stack .NET Engineer  
擅長系統架構設計、資料建模與任務自動化。  
專注於讓後端邏輯更聰明、前後端整合更高效。  

> _This demo is for technical showcase only. All names, data, and screens are fictional._

---

