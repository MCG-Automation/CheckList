# SESSION_LOG.md — Tiến độ theo session

# Claude Code tự cập nhật file này. Không sửa thủ công phần log.

---

## Session 2026-06-16 (3) — UserGuide v2.0

### Đã làm
- Viết lại `Docs/Macgregor_CheckList_UserGuide.html` — v2.0:
  - Style hiện đại giống MCGVN_Autocad_Inventor_Installation_Guide (DM Sans font, navy/red theme, card layout, sticky tab nav)
  - 6 tab: Tổng quan · AutoCAD · Inventor · Tính năng chi tiết · FAQ · Vault Integration
  - Bao gồm cả AutoCAD (DWG) và Inventor (IDW) trong một file duy nhất
  - Mô tả đầy đủ: dropdown chọn loại bản vẽ, progress bar, auto-save 500ms, carry-over, custom items, approve/re-open, sync status
  - FAQ accordion 10 câu (5 AutoCAD + 5 Inventor)
  - Vault Integration roadmap 4 bước
  - So sánh bảng AutoCAD vs Inventor
  - Data flow diagram ưu tiên cache

### Trạng thái
- File: `Docs/Macgregor_CheckList_UserGuide.html` ✅ hoàn thành

---

## Session 2026-06-16 (2) — Checklist restore theo bản vẽ + bỏ auto-load

### Đã làm
- [Models/CheckList/CheckList.Models.cs](Models/CheckList/CheckList.Models.cs): Thêm `ExcelFileName` vào `ChecklistDocument` — lưu loại checklist nào đã dùng
- [Views/CheckList/CheckList.View.xaml](Views/CheckList/CheckList.View.xaml): Bỏ `IsSelected="True"` khỏi ComboBoxItem; thêm empty-state placeholder "Select a drawing type above"
- [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs):
  - Thêm `_suppressSelectionChanged` flag
  - Refactor `TriggerInitialSelection`: đọc DWG → nếu có data thì tự chọn đúng ComboBox item và load; nếu không có thì để trống chờ user
  - `CboDiscipline_SelectionChanged`: thoát sớm khi `_suppressSelectionChanged = true`
  - `LoadChecklistFileAsync`: lưu `loadedDoc.ExcelFileName = filePathOrName` sau khi load thành công

### Hành vi sau khi fix
- Mở tool với bản vẽ mới → hiện empty state "Select a drawing type above"
- User chọn "Structure" → load checklist Structure, lưu vào DWG
- Tắt máy, hôm sau mở lại bản vẽ → tool tự động chọn "Structure" và khôi phục toàn bộ dấu tích

### Build: PASS (0 errors, 0 warnings)

---

## Session 2026-06-16 — Wire-up DWG persistence cho CheckList

### Đã làm
- [Services/CheckList/IAutoCadChecklistService.cs](Services/CheckList/IAutoCadChecklistService.cs): **Tạo mới** — Interface cho DWG read/write service
- [Services/CheckList/CheckList.Main.cs](Services/CheckList/CheckList.Main.cs): `AutoCadService` implement `IAutoCadChecklistService`
- [Services/CheckList/IChecklistOrchestrator.cs](Services/CheckList/IChecklistOrchestrator.cs): Thêm `dwgPreload` parameter vào `OpenChecklist`
- [Services/CheckList/ChecklistOrchestrator.cs](Services/CheckList/ChecklistOrchestrator.cs): Ưu tiên DWG data hơn JSON cache trong Carry-over
- [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs): Thêm `_autocadService`, load DWG trước Task.Run, save DWG trong debounce timer / Approve / Reopen; thêm `SaveToDwgSilent()` helper

### Trạng thái
- Phase: CheckList — DWG persistence hoàn chỉnh
- Build: **PASS** (0 errors, 0 warnings mới)

### Ưu tiên nguồn dữ liệu khi mở bản vẽ
1. DWG XRecord (portable, đi theo file) → **ưu tiên cao nhất**
2. JSON cache %APPDATA% (local machine) → fallback nếu DWG chưa có data
3. Excel template → fallback cuối (fresh start)

### Khi nào lưu vào DWG
- Auto-save debounce 500ms sau mỗi tick/untick
- Ngay lập tức khi Approve hoặc Reopen
- Silent (không block UI nếu lỗi, chỉ log)

### Bước tiếp theo
- Test: mở bản vẽ, tích 20%, lưu DWG (Ctrl+S), mở lại → verify dữ liệu được khôi phục
- Test: copy DWG sang máy khác → verify checklist đi theo file

---

## Session 2026-06-15 (2) — Fix Vault login: strip http://, fix ExcelParser resource name

### Đã làm
- [Services/CheckList/VaultSyncService.cs](Services/CheckList/VaultSyncService.cs):
  - **Fix root cause "Vault offline"**: `settings.VaultServer = "http://VNHPH1-S0006"` → Vault SDK chỉ nhận hostname. Thêm logic strip `http://` / `https://` trước khi gọi `LogIn`.
  - Bỏ `GetActiveConnections()` (không tồn tại trên Vault 2023 SDK `IVaultConnectionManagerService`).
  - Log chi tiết `server=` + `vault=` trước khi connect để dễ debug.
  - `LogInResult.ErrorMessages` được join và ném vào exception message để thấy lý do thất bại.
  - Wrap `LogOut` trong try-catch để tránh ẩn lỗi từ bước download.
- [Services/CheckList/ExcelChecklistParser.cs](Services/CheckList/ExcelChecklistParser.cs):
  - `ExtractDefaultTemplate`: bỏ hardcode `"MCGCadPlugin.Resources.DefaultChecklist.xlsx"`, thay bằng `EndsWith(fileName)` tìm resource khớp tên file thực (per-discipline).
  - Log danh sách tất cả embedded resources khi không tìm thấy.
- [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs):
  - Lỗi `FileNotFoundException` từ parser: hiển thị message rõ ràng với đường dẫn cần copy file + 3 giải pháp.
- Build: 0 errors.

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- Vault login bây giờ sẽ dùng đúng hostname `VNHPH1-S0006` thay vì `http://VNHPH1-S0006`.
- Nếu vẫn fail sau fix → xem Debug Output để đọc `LogInResult.ErrorMessages`.

### Bước tiếp theo (BẮT BUỘC — user action)
- Copy 3 file Excel vào `Resources/` folder để có embedded fallback:
  - `Temp Checklist - LayoutInterface.xlsx`
  - `Temp Checklist - Mechanical.xlsx`
  - `Temp Checklist - Structure.xlsx`
  - Nguồn: `C:\MacGregor_CAS_WF\Designs\90 Users\truonph\`
  - Sau khi copy: `dotnet build -c Debug` để embed vào DLL.

### Ghi chú API
- **Vault SDK `LogIn` server format**: hostname thuần (`VNHPH1-S0006`), không có `http://`. URL prefix khiến login fail âm thầm.
- **`IVaultConnectionManagerService.GetActiveConnections()`**: không tồn tại trong Vault 2023 SDK — không thể tái dùng connection từ process khác (Vault Explorer).
- **Embedded resource suffix match**: `assembly.GetManifestResourceNames()` trả tên đầy đủ như `MCGCadPlugin.Resources.Temp Checklist - Structure.xlsx`. Dùng `EndsWith(fileName)` thay vì equals để tránh sai prefix.

---

## Session 2026-06-15 — Vault sync: refactor VaultSyncResult + UI status indicator

### Đã làm
- [Models/CheckList/VaultSyncResult.cs](Models/CheckList/VaultSyncResult.cs) — TẠO MỚI: model trả về từ VaultSyncService, có `LocalPath`, `SyncedFromVault` (bool), `ErrorMessage`.
- [Models/CheckList/CheckList.Models.cs](Models/CheckList/CheckList.Models.cs) — Thêm 2 property vào `ChecklistDocument`: `SyncedFromVault` (bool) + `SyncMessage` (string).
- [Services/CheckList/IVaultSyncService.cs](Services/CheckList/IVaultSyncService.cs) — Đổi return type `string` → `VaultSyncResult`.
- [Services/CheckList/VaultSyncService.cs](Services/CheckList/VaultSyncService.cs) — Trả `VaultSyncResult` thay vì `string`. Success path: `SyncedFromVault=true`. Fallback path: `SyncedFromVault=false` + `ErrorMessage`.
- [Services/CheckList/ChecklistOrchestrator.cs](Services/CheckList/ChecklistOrchestrator.cs) — Dùng `VaultSyncResult`, set `newDoc.SyncedFromVault` + `newDoc.SyncMessage` trước khi trả về.
- [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs) — Thêm properties `SyncStatusText` (string) + `SyncStatusColor` (Brush); set sau khi load xong: "Vault ✔ HH:mm" (xanh) / "Vault offline — local copy" (cam) / "Local file" (xám).
- [Views/CheckList/CheckList.View.xaml](Views/CheckList/CheckList.View.xaml) — Thêm TextBlock dòng thứ 3 trong StackPanel tiêu đề; Collapsed khi SyncStatusText rỗng.
- Build: 0 errors, 1 warning Fody (pre-existing).

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- **Vault path đã xác nhận đúng:** `$/Designs/90 Users/{username}` ↔ `C:\MacGregor_CAS_WF\Designs\90 Users\{username}` — không cần sửa ChecklistSettings.
- Flow Get Latest từ Vault đã hoạt động từ trước; session này chỉ thêm feedback UI rõ ràng.

### Bước tiếp theo
- Test trong AutoCAD: mở Palette → quan sát dòng trạng thái dưới "MacGregor Quality Control". Kỳ vọng: "Vault ✔ 14:32" màu xanh khi online; "Vault offline — local copy" màu cam khi mất kết nối.
- (Tùy chọn) Bước 2: Thêm Settings dialog để admin thay đổi VaultServer/VaultName/VaultExcelFolderPath không cần sửa settings.json tay.

### Ghi chú
- `SyncedFromVault` và `SyncMessage` trên `ChecklistDocument` không được serialize vào cache JSON (giá trị tính toán mỗi lần load, không cần lưu lại).
- Khi `useVault=false` (manual load từ nút "..."), SyncStatusText = "Local file" màu xám — user biết họ đang dùng file chọn tay, không qua Vault.

---

## Session 2026-05-07 (2) — Update Macgregor_CheckList_UserGuide.html cho khớp hiện trạng

### Đã làm
[Docs/Macgregor_CheckList_UserGuide.html](Docs/Macgregor_CheckList_UserGuide.html) — 5 chỗ sửa:
1. **Intro paragraph** ([line 115]): bỏ "và Autodesk Inventor" (plugin chỉ AutoCAD), thêm note "Phiên bản Inventor: roadmap".
2. **Section 1 — Cài đặt** ([line 121-130]): tách thành 2 cách: (1) Auto-load qua `Install_AutoLoadCadAddin.bat` (khuyến nghị), (2) NETLOAD thủ công cho test/debug. Đổi `MCGCadPlugin.dll` → `MCG_Checklist.dll`. Đổi command ví dụ `MCG_Fitting` → `MCG_Checklist`.
3. **Section 2 Bước 3 — Sign & Approve**: button label `SIGN & APPROVE DRAWING` → `SIGN & APPROVE`. **Xoá** mention về stamp "CheckList Passed" trên layer Defpoints (tính năng đã gỡ ở session 2026-05-04 (2)). Thay bằng mô tả trạng thái thực tế: MessageBox confirmation + header `APPROVED (READY FOR RELEASE)` trên Palette.
4. **Section 3 — Vault integration**: thêm badge `ROADMAP — CHƯA TRIỂN KHAI` + warning box giải thích trạng thái hiện tại (DBDictionary nội bộ Vault không đọc được). Bổ sung **Bước 0** (Dev): viết bridge property `MacGregor_QA` vào `Database.SummaryInfo`/`DwgProperty` khi Approve để Vault map qua tên đó. 4 bước thay vì 3.
5. **Section 4 — FAQ rewrite hoàn toàn**: xoá Q về Inventor + Q về "Auto-Purge stamp giả mạo" (tính năng đã gỡ). Thay bằng 5 Q mới khớp UI hiện tại:
   - Q1: `MCG_Checklist` báo Unknown command — kiểm tra bundle/NETLOAD/log.
   - Q2: Đóng AutoCAD giữa chừng có mất data không — không, lưu vào DBDictionary nội file DWG.
   - Q3: Làm sao biết bản vẽ đã Approve — header DRAWING STATUS có 2 state APPROVED/PENDING.
   - Q4: Sửa bản vẽ đã Approve — bấm Reset / Clear Data, mô tả flow cụ thể.
   - Q5 (giữ nguyên): Phân biệt bản vẽ Main vs Global.

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- User guide nay đã đồng bộ với code thực tế: tên DLL `MCG_Checklist.dll`, lệnh `MCG_Checklist`, button `SIGN & APPROVE`, không còn mention QA Stamp / Auto-Purge.
- Vault integration được giữ ở guide làm bản thiết kế tham chiếu (roadmap), không phải tính năng đã ship.

### Bước tiếp theo
- Khi implement Vault bridge property (Bước 0): viết vào `Database.SummaryInfo.SetCustomProperty("MacGregor_QA", "APPROVED")` trong `BtnSignApprove_Click` sau `SaveChecklistToDwg`. Reset thì xoá property.
- Cân nhắc đổi badge color/style của ROADMAP nếu user feedback.

### Ghi chú API
- DBDictionary trong DWG (`MACGREGOR_QA_SYSTEM`/`CHECKLIST_DATA`) là dữ liệu nội bộ — Vault không đọc được. Vault chỉ map iProperty/Custom DwgProperty (qua `Database.SummaryInfo`).
- Nếu sau này cần Vault integration: tách rõ 2 layer — DBDictionary cho UI/logic phong phú, DwgProperty cho 1 cờ trạng thái mà Vault đọc.

---

## Session 2026-05-07 — Align convention naming + bundle path với HTML guide deploy

### Đã làm
- [MCGCadPlugin.csproj](MCGCadPlugin.csproj): `<PluginName>MCGCadPlugin.CheckList</PluginName>` → `<PluginName>MCG_Checklist</PluginName>`. DLL output đổi thành `MCG_Checklist.dll` (Release) / `MCG_Checklist_<timestamp>.dll` (Debug). Khớp wildcard `MCG_*.dll` mà bat trong HTML guide quét.
- [CLAUDE.md](CLAUDE.md) mục 2: `Bundle folder: %APPDATA%\Autodesk\ApplicationPlugins\` → `%PROGRAMDATA%\Autodesk\ApplicationPlugins\`. Khớp với bat shared `Install_AutoLoadCadAddin.bat` (HTML guide) — All Users, gộp tất cả `MCG_*.dll` vào 1 bundle `MCG_Plugin.bundle`.
- [Commands/PaletteManager.cs](Commands/PaletteManager.cs): PaletteSet title `"MCGCadPlugin - CheckList"` → `"MCG Checklist"`.
- Build: `dotnet build -c Debug` → 0 warnings, 0 errors. DLL output: `MCG_Checklist_20260507_142632.dll`.

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- **Convention chính thức (deploy artifacts):**
  - DLL: `MCG_<Module>.dll` (Release) / `MCG_<Module>_<timestamp>.dll` (Debug). Module hiện tại: `Checklist`.
  - PaletteSet title: `MCG <Module>` (vd: `"MCG Checklist"`).
  - Lệnh CAD: `MCG_<Module>` (đã có: `MCG_Checklist`).
  - Bundle: gộp **shared bundle** `MCG_Plugin.bundle` ở `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` (Option Y — bat shared scan toàn bộ `MCG_*.dll` trên drive, do team Dev đồng bộ).
  - C# namespace giữ nguyên `MCGCadPlugin.<Layer>.<Module>` (theo CLAUDE.md mục 3) — namespace là cấu trúc code, độc lập với DLL filename.

### Bước tiếp theo
- User test: đóng AutoCAD, build lại, copy `MCG_Checklist*.dll` vào `C:\CustomTools\Autocad\`, chạy `Install_AutoLoadCadAddin.bat` (Run as Admin). Mở AutoCAD → kiểm tra autoload, gõ `MCG_Checklist` → Palette title hiển thị `"MCG Checklist"`.
- Bundle cũ `MCGCadPlugin.CheckList.bundle` (nếu đã từng deploy) cần xoá tay ở `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` để tránh load song song hai version.

### Ghi chú API
- HTML guide `MCGVN_Autocad_Inventor_Installation_Guide.html` **không sửa** — đây là guide generic cho team Dev (1 bat shared cho tất cả MCG plugin). Plugin phải align convention vào guide, không ngược lại.
- `<PluginName>` trong csproj điều khiển `<AssemblyName>` (qua condition Debug/Release ở [csproj:16-17](MCGCadPlugin.csproj#L16-L17)) — đổi 1 chỗ, kéo theo DLL filename + bundle scan + load dynamic.
- Khi đổi PaletteSet title nhưng GUID giữ nguyên (`7b3e9a2c-...`) → AutoCAD vẫn nhớ vị trí dock cũ; chỉ tên hiển thị thay đổi.

---

## Session 2026-05-04 (4) — Đơn giản hoá tên command CAD

### Đã làm
- [Commands/CheckListCommands.cs](Commands/CheckListCommands.cs):
  - `[CommandMethod("MCG_Checklist_Show")]` → `[CommandMethod("MCG_Checklist")]` (bỏ hậu tố `_Show`).
  - Xoá hẳn method `Hide()` và `[CommandMethod("MCG_Checklist_Hide")]`. Class giờ chỉ còn 1 lệnh CAD duy nhất.
- [CLAUDE.md](CLAUDE.md) (mục 9 — Lệnh CAD): cập nhật danh sách lệnh thành chỉ `MCG_Checklist`, kèm note "ẩn bằng nút Close trên UI".
- `PaletteManager.Hide()` (method) **giữ nguyên** — vẫn được gọi từ `BtnClosePalette_Click` trong [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs). Chỉ bỏ `[CommandMethod]` wrapper.
- Build: `dotnet build -c Debug` → 0 warnings, 0 errors.

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- **Lệnh CAD chính thức:** chỉ còn `MCG_Checklist` (mở Palette). Đóng Palette = nút Close trên UI.

### Bước tiếp theo
- Test trong AutoCAD: gõ `MCG_Checklist` → Palette mở; bấm nút Close → Palette ẩn. Gõ `MCG_Checklist_Show` / `MCG_Checklist_Hide` → "Unknown command" (đúng kỳ vọng).

### Ghi chú API
- Bỏ `[CommandMethod]` không xoá method khỏi class — chỉ bỏ đăng ký lệnh CAD. Method `PaletteManager.Hide()` vẫn callable từ code C#.

---

## Session 2026-05-04 (3) — Fix 4 build warnings

### Đã làm
[MCGCadPlugin.csproj](MCGCadPlugin.csproj):
- **Fix Fody warning**: Xoá `<IncludeAssets>` trong `PackageReference Costura.Fody` (giữ `<PrivateAssets>all</PrivateAssets>`). Theo khuyến nghị chính thức của Fody.
- **Fix MSB3073 (PowerShell exit 9009)**: Xoá hẳn `<Target Name="UpdatePackageContents">` (cũ, lines 25-49). Lý do: file `PackageContents.xml` không tồn tại trong repo → target dead code; multi-line PS Exec command gây cmd.exe parse fail.
- **Hạ MSB3061 (DLL bị AutoCAD khoá)**: Thêm `<MSBuildWarningsAsMessages>MSB3061</MSBuildWarningsAsMessages>`. Build mới dùng timestamp filename nên không thực sự bị block — đây chỉ là noise khi MSBuild cố cleanup DLL cũ. Hạ thành message.
- Build: `dotnet build -c Debug` → **0 warnings, 0 errors**.

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- Build hoàn toàn sạch.

### Bước tiếp theo
- Tiếp tục các task Phase 1 (chưa xác định cụ thể).

### Ghi chú API
- **MSB3061 từ AutoCAD lock**: tới từ MSBuild's `IncrementalClean` cố xoá output cũ. Không thể fix bằng config thuần — chỉ có thể (a) đóng AutoCAD trước khi build, hoặc (b) suppress qua `MSBuildWarningsAsMessages`. Đã chọn (b).
- **`MSBuildWarningsAsMessages` vs `NoWarn`**: `NoWarn` ẩn hoàn toàn; `MSBuildWarningsAsMessages` chuyển warning thành message (vẫn hiển thị nếu verbosity đủ cao). Chọn cái sau để giữ visibility khi cần debug.
- **Costura.Fody best practice**: chỉ cần `<PrivateAssets>all</PrivateAssets>`. Đặt `<IncludeAssets>` thiếu `compile` sẽ phát warning ở MSBuild restore phase.

---

## Session 2026-05-04 (2) — Bỏ tính năng QA Stamp trên bản vẽ

### Đã làm
- Xoá file [Services/CheckList/CheckList.Stamp.cs](Services/CheckList/CheckList.Stamp.cs) (cũ): bỏ `GenerateQaStamp()` và `PurgeFakeQaStamps()`.
- [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs):
  - `RefreshStatus()` (else branch): bỏ call `_acService.PurgeFakeQaStamps()` và comment AUTO-PURGE.
  - `BtnReset_Click`: bỏ call `_acService.PurgeFakeQaStamps()`; sửa MessageBox `"QA/QC data and CAD stamps cleared successfully."` → `"QA/QC data cleared successfully."`.
  - `BtnApprove_Click`: bỏ call `_acService.GenerateQaStamp()`. MessageBox `"Drawing successfully Approved and Signed!"` giữ nguyên (đủ feedback theo user xác nhận).
- Build: `dotnet build -c Debug` → 0 errors.

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- **Lý do bỏ:** stamp MText trên layer Defpoints chỉ là visual mark + chống giả mạo. Status APPROVED thực sự nằm trong XRecord (CheckList.Database.cs); không có code nào check status qua sự tồn tại của MText. Bỏ đi không phá nghiệp vụ.
- **Tradeoff đã chấp nhận:** mất visible mark trên bản vẽ — người mở file phải mở Palette để biết status.

### Bước tiếp theo
- Test trong AutoCAD: Approve drawing → chỉ thấy MessageBox, không có MText "CheckList Passed" sinh ra trên layer Defpoints. Reset → data XRecord được xoá, không cần purge MText.

### Ghi chú API
- `partial class AutoCadService` được tách 3 file (Main / Database / Stamp). Xoá Stamp.cs an toàn vì 2 file còn lại không tham chiếu chéo.
- Layer `Defpoints` là layer hệ thống AutoCAD, luôn tồn tại — không cần code tự tạo.

---

## Session 2026-05-04 — Fix MissingMethodException khi gõ lệnh CAD

### Đã làm
- Tạo mới [Commands/CheckListCommands.cs](Commands/CheckListCommands.cs): class `CheckListCommands` với public parameterless ctor, chứa 2 `[CommandMethod]` (`MCG_Checklist_Show` / `MCG_Checklist_Hide`) — uỷ quyền cho `PaletteManager.Instance`.
- [Commands/PaletteManager.cs](Commands/PaletteManager.cs): xoá block `#region AutoCAD Commands` (bỏ `McgShow`/`McgHide`); xoá `using Autodesk.AutoCAD.Runtime;` không còn dùng. Singleton giờ thuần Palette logic.
- Build: `dotnet build -c Debug` → 0 errors.

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- **Lỗi đã sửa:** `System.MissingMethodException: No parameterless constructor defined for this object` ném từ `Activator.CreateInstance` trong `PerDocumentCommandClass.Invoke`. Nguyên nhân: `[CommandMethod]` đặt trên instance method của Singleton (ctor private) — AutoCAD không thể tạo instance per-document.

### Bước tiếp theo
- User đóng AutoCAD (DLL đang bị khoá theo warning build), build lại, NETLOAD DLL mới, gõ `MCG_Checklist_Show` / `MCG_Checklist_Hide` để xác nhận hết lỗi.
- Nếu OK → tiếp tục các task Phase 1.

### Ghi chú API
- **AutoCAD `[CommandMethod]` trên instance method** → AutoCAD luôn `Activator.CreateInstance(type)` để tạo instance mới mỗi lần gọi. Class command BẮT BUỘC có public parameterless constructor.
- Pattern chuẩn cho Singleton + Commands: tách 2 class. Singleton (ctor private) lo state/lifecycle; class Commands riêng (ctor public mặc định) chỉ uỷ quyền sang Singleton.
- Alternative: dùng `static` cho method `[CommandMethod]` thì không cần instance — nhưng vẫn tách class command riêng để giữ SRP.

---

## Session 2026-04-21 (4) — Rename plugin (CheckList)

### Đã làm
- [MCGCadPlugin.csproj](MCGCadPlugin.csproj): `<PluginName>MCGCadPlugin</PluginName>` → `<PluginName>MCGCadPlugin.CheckList</PluginName>` → DLL output đổi thành `MCGCadPlugin.CheckList_<timestamp>.dll`.
- [Commands/PaletteManager.cs](Commands/PaletteManager.cs): PaletteSet title `"MCG Plugins"` → `"MCGCadPlugin - CheckList"`.
- [CLAUDE.md](CLAUDE.md): nới rule §2 (hạn chế chứ không cấm tuyệt đối sửa csproj khi có lý do rõ ràng).

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- **Command names:** đã đổi ở session trước (`MCG_Checklist_Show` / `MCG_Checklist_Hide`) — không đổi lần này.

### Bước tiếp theo
- Test: gõ `MCG_Checklist_Show` / `MCG_Checklist_Hide` trong AutoCAD, xác nhận title `"MCGCadPlugin - CheckList"` và DLL mới `MCGCadPlugin.CheckList_*.dll`.
- Load song song với plugin FittingManagement — phải thấy 2 PaletteSet riêng biệt.

### Ghi chú API
- Dùng dấu chấm (`.CheckList`) cho `PluginName` để an toàn với bundle/PackageContents/PowerShell regex trong target `UpdatePackageContents`.

---

## Session 2026-04-21 (3) — Đổi PaletteGuid và tên lệnh cho CheckList (tránh conflict với FittingManagement)

### Đã làm
- [Commands/PaletteManager.cs](Commands/PaletteManager.cs):
  - `PaletteGuid`: `2b80cfe9-c560-49d6-8a09-9d636260fcf2` → `7b3e9a2c-4d81-4f75-a63e-5c29d8b41f07` (GUID v4 mới, riêng cho CheckList).
  - `[CommandMethod("MCG_Show")]` → `[CommandMethod("MCG_Checklist_Show")]`.
  - `[CommandMethod("MCG_Hide")]` → `[CommandMethod("MCG_Checklist_Hide")]`.
- [CLAUDE.md](CLAUDE.md): cập nhật §9 (GUID sample) và dòng danh sách "Lệnh CAD".

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- **Mục đích:** cho phép load đồng thời plugin CheckList và plugin FittingManagement trong cùng phiên AutoCAD mà không xung đột palette/command.

### Bước tiếp theo
- Build + test: load cả 2 plugin trong AutoCAD, gõ `MCG_Show` (mở FittingManagement) và `MCG_Checklist_Show` (mở CheckList) để xác nhận 2 PaletteSet riêng biệt.

### Ghi chú API
- GUID quyết định AutoCAD nhớ vị trí dock — đã deploy trước đó với GUID cũ, việc đổi sẽ reset vị trí dock lần đầu user chạy version mới.
- `[CommandMethod]` attribute phải unique trong toàn bộ runtime AutoCAD — plugin load sau sẽ override nếu trùng tên.

---

## Session 2026-04-21 (2) — Tách CheckList thành repo riêng

### Đã làm
- Tách nội dung module CheckList khỏi repo FittingManagement, push lên `https://github.com/MCG-Automation/CheckList.git` (branch `main`).
- Xóa FittingManagement (Commands/Models/Services/Views/Utilities) và `Docs/Macgregor_FittingTool_UserGuide.html`.
- [Commands/PaletteManager.cs](Commands/PaletteManager.cs): bỏ using FittingManagement, Initialize() chỉ còn 1 `AddVisual` cho CheckList.
- [CLAUDE.md](CLAUDE.md): cập nhật §3 namespace tree và §9 PaletteSet (1 Module — 1 Tab) cho scope repo này.

### Trạng thái
- **Phase:** 1 — Feature Implementation (repo standalone cho CheckList).
- **Repo gốc lịch sử:** forked từ FittingManagement repo — giữ full git history.

### Bước tiếp theo
- Đổi GUID PaletteSet nếu sẽ dùng song song với plugin FittingManagement (hiện cả 2 repo đều dùng `2b80cfe9-c560-49d6-8a09-9d636260fcf2`).
- Cân nhắc đổi tên command `MCG_Show` / `MCG_Hide` (ví dụ: `MCG_Checklist_Show`) để tránh conflict khi load cả 2 plugin cùng lúc trong AutoCAD.

### Ghi chú API
- Repo này chia sẻ `Utilities/FileLogger.cs` và `Commands/PaletteManager.cs` với repo FittingManagement. Khi sửa các file dùng chung, cần sync thủ công giữa 2 repo.

---

## Session 2026-04-21 — Xóa 4 module (giữ CheckList + FittingManagement), tách repo

### Đã làm
- Xóa 20 folder module (Commands/Models/Services/Views/Utilities × 4 module: DetailDesign, PanelData, TableOfContent, Weight).
- [Commands/PaletteManager.cs](Commands/PaletteManager.cs): bỏ 4 `using` Views của các module đã xóa, rút gọn `Initialize()` còn 2 `AddVisual` (Fitting Management, CheckList), cập nhật comment "5 tabs" → "2 tabs".
- [CLAUDE.md](CLAUDE.md): cập nhật §3 namespace tree và §9 PaletteSet section để phản ánh kiến trúc 2 module.
- Thêm remote `fittingmgmt` → push sang `https://github.com/MCG-Automation/FittingManagement.git` (branch `main`).

### Files giữ nguyên
- `CheckList` (Models/Services/Views) — không có Commands, không có Utilities.
- `FittingManagement` (Commands/Models/Services/Views/Utilities).
- `Commands/PaletteManager.cs` (sửa), `Utilities/FileLogger.cs` (shared), `Docs/`, `Resources/`, `MCGCadPlugin.csproj` (không sửa — SDK-style tự include theo thư mục).

### Trạng thái
- **Phase:** 1 — Feature Implementation (scope giới hạn lại còn 2 module).
- **Build:** Succeeded — 0 errors, 5 warnings (pre-existing: AutoCAD lock DLL cũ, Fody IncludeAssets, PowerShell escape trong target UpdatePackageContents).

### Bước tiếp theo
- Xác nhận repo `FittingManagement` trên GitHub đã nhận được code.
- Test trong AutoCAD: `MCG_Show` → palette phải có đúng 2 tab "Fitting Management" và "CheckList".

### Ghi chú API
- `.csproj` dùng `Microsoft.NET.Sdk` nên tự include source theo thư mục — xóa folder không cần sửa csproj.

---

## Session 2026-04-20 (4) — Đăng ký lệnh AutoCAD cho Palette

### Đã làm
- Thêm `[CommandMethod("MCG_Show")]` và `[CommandMethod("MCG_Hide")]` vào Commands/PaletteManager.cs.
- Các lệnh này gọi trực tiếp đến instance Singleton để điều khiển hiển thị `PaletteSet`.

### Trạng thái
- **Phase:** 1 — Feature Implementation.
- **Build:** Succeeded.

### Bước tiếp theo
- **File:** `Views/DetailDesign/DetailDesignViewModel.cs` | Triển khai ViewModel mẫu cho module DetailDesign.
- **Build & Test:** Chạy file `build-and-launch.bat` để kiểm tra các lệnh mới trong AutoCAD.

### Ghi chú API
- `CommandFlags.Modal` được sử dụng cho lệnh hiển thị Palette để đảm bảo tính ổn định khi gọi từ command line.
- Do `PaletteManager` là Singleton, các phương thức `CommandMethod` không cần static nếu class được AutoCAD khởi tạo đúng cách, nhưng ở đây tôi để instance method gọi qua singleton để nhất quán.

---

## Session 2026-04-20 (3) — Checklist: Remove (đã có) + N/A cho custom items

### Đã làm
- **Remove item**: xác nhận chức năng đã có sẵn — nút `X` ở mỗi dòng chỉ hiển thị khi `IsCustom=true` (Visibility binding qua `BooleanToVisibilityConverter`). Fixed items không có nút X → không xóa được. Giữ nguyên, không thay đổi.
- **N/A cho custom items (mới)**:
  - [Models/CheckList/CheckList.Models.cs](Models/CheckList/CheckList.Models.cs): thêm property `IsNotApplicable`, implement `INotifyPropertyChanged` trên `ChecklistItem`. Setter của `IsChecked` và `IsNotApplicable` có **mutual exclusion** — bật cái này tự tắt cái kia.
  - [Views/CheckList/CheckList.View.xaml](Views/CheckList/CheckList.View.xaml): data template tăng từ 3 cột lên 4 cột (IsChecked | Content | N/A | Delete). Checkbox N/A dùng chung binding `BooleanToVisibilityConverter` với nút X → chỉ hiện cho custom items. Thêm `DataTrigger` trên TextBlock để **strikethrough + xám + italic** khi `IsNotApplicable=true`.
  - [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs): thêm handler `NaCheckBox_Click`; đổi logic `UpdateProgress` đếm `IsChecked || IsNotApplicable` là "done" → Sign & Approve enable khi tất cả items đều satisfied.

### Trạng thái
- **Phase:** 1 — Feature Implementation (Checklist module).
- **Build:** Succeeded — 0 errors, 5 warnings (pre-existing).

### Bước tiếp theo
- Test trong AutoCAD: mở checklist → thêm custom item → tick N/A → xác nhận text gạch ngang + Sign & Approve enable khi tất cả items đều satisfied.
- Test backward-compat: mở bản vẽ cũ có checklist đã lưu trước đó → `IsNotApplicable` default = false, không vỡ JSON.

### Ghi chú API
- JSON.NET trên .NET Framework 4.8 serialize/deserialize class có `INotifyPropertyChanged` bình thường (auto-properties và backing fields đều OK). Không cần `[JsonIgnore]` cho sự kiện `PropertyChanged`.
- Mutual exclusion phải thực hiện ở setter **sau khi** `OnPropertyChanged` của property gốc đã được raise, để tránh binding WPF bị nhầm trình tự cập nhật.

---

## Session 2026-04-20 (2) — Thêm nút X (Close) đóng Palette trong QaChecklistView

### Đã làm
- Thêm nút `BtnClosePalette` (ký tự `X`, 24×24) ở **góc trên phải** của [Views/CheckList/CheckList.View.xaml](Views/CheckList/CheckList.View.xaml) — `Grid.Row=0`, `HorizontalAlignment=Right`.
- Shift các row hiện có xuống: Status GroupBox → Row 1, action buttons → Row 2, `PanelChecklist` → Row 3. Tổng grid nay 4 rows (`Auto, Auto, Auto, *`).
- Thêm handler `BtnClosePalette_Click` trong [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs) gọi `PaletteManager.Instance.Hide()` (import thêm `MCGCadPlugin.Commands`).

### Trạng thái
- **Phase:** 1 — Feature Implementation (Checklist module).
- **Build:** Succeeded — 0 errors.

### Bước tiếp theo
- Test trong AutoCAD: `MCG_Show` → tab Checklist → bấm X → xác nhận Palette ẩn; `MCG_Show` lại → Palette hiện lại đúng trạng thái.

### Ghi chú API
- `PaletteManager.Instance.Hide()` chỉ set `_paletteSet.Visible = false` — không dispose, nên state tab + control giữ nguyên khi mở lại.

---

## Session 2026-04-20 (1) — Gộp ChecklistWindow vào QaChecklistView

### Đã làm
- **Gộp cửa sổ modal `ChecklistWindow` thành panel inline** trong `QaChecklistView`:
  - Panel checklist (header, progress bar, list items, add custom, Save/Approve buttons) nay nằm ở `Grid.Row=2` của `QaChecklistView`, `Visibility=Collapsed` mặc định.
  - Nút `OPEN CHECKLIST` hiển thị panel; nút `Cancel` ẩn panel; `Save Draft` / `SIGN & APPROVE` lưu rồi ẩn panel + `RefreshStatus()`.
  - Khi user đổi Discipline trước khi bấm OPEN → nạp lại default items tương ứng (chỉ khi chưa APPROVED).
  - Khi `_currentDoc.Status == "APPROVED"`: khoá list, ẩn Save Draft, đổi nút thành `ALREADY APPROVED` (giữ hành vi gốc).
- **Xóa 2 file không còn dùng**: `Views/CheckList/CheckList.Window.xaml` và `CheckList.Window.xaml.cs`.
- Thêm `LOG_PREFIX = "[QaChecklistView]"`, thêm `BooleanToVisibilityConverter` vào `UserControl.Resources`.

### Files đã sửa
- [Views/CheckList/CheckList.View.xaml](Views/CheckList/CheckList.View.xaml) — thêm `PanelChecklist` inline.
- [Views/CheckList/CheckList.View.xaml.cs](Views/CheckList/CheckList.View.xaml.cs) — merge toàn bộ logic của `ChecklistWindow`.

### Files đã xóa
- `Views/CheckList/CheckList.Window.xaml`
- `Views/CheckList/CheckList.Window.xaml.cs`

### Trạng thái
- **Phase:** 1 — Feature Implementation (Checklist module).
- **Build:** Succeeded — 0 errors, 2 warnings (pre-existing, không liên quan).

### Bước tiếp theo
- Test trong AutoCAD: `MCG_Show` → tab Checklist → `OPEN CHECKLIST` → tick items → `Save Draft` / `SIGN & APPROVE` → kiểm tra status refresh đúng, không còn cửa sổ modal.
- Cân nhắc di chuyển logic ra ViewModel (MVVM) khi mở rộng thêm tính năng.

### Ghi chú API
- `Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(window)` không còn cần thiết — panel nằm trực tiếp trong PaletteSet nên không bị chặn tương tác với bản vẽ khi cần.

---

## Session 2026-04-10 (1) — Project Audit

### Đã làm
- Thực hiện **Audit dự án** theo skill `audit-project`.
- Đối soát 5 lớp kiến trúc (Commands, Models, Services, Views, Utilities) cho 5 module.

### Trạng thái thực tế
| File/Folder | Tình trạng | Ghi chú |
|---|---|---|
| `Views/*.xaml` | ✅ OK | 5 UserControl đã sẵn sàng. |
| `ViewModels/` | ❌ THIẾU | Chưa triển khai MVVM hoàn chỉnh. |
| `Interfaces/` | ❌ THIẾU | Mới chỉ có cho FittingManagement. |
| `Commands/` | ⚠️ Cần sửa | Thiếu đăng ký CommandMethod cho Palette. |
| `_Template/` | ❌ THIẾU | Chưa có folder mẫu cho các layer. |

### Trạng thái
- **Phase:** 1 — Feature Implementation (Bị nghẽn do thiếu ViewModel).
- **Build:** Succeeded (nhưng code chỉ là placeholder).

### Bước tiếp theo
1. **File:** `Views/DetailDesign/DetailDesignViewModel.cs` | Tạo ViewModel đầu tiên làm mẫu.
2. **File:** `Commands/PaletteManager.cs` | Thêm `[CommandMethod]` cho `MCG_Show` và `MCG_Hide`.
3. **File:** `Services/_Template/ITemplateService.cs` | Tạo bộ template chuẩn.

### Ghi chú API
- Cần chú ý việc bind `DataContext` của View vào ViewModel trong code-behind của UserControl.
- PaletteSet yêu cầu các lệnh Show/Hide phải nằm trong một class được AutoCAD nhận diện (thường là static hoặc singleton).

---

## Session 2026-04-09 (2) — Triển khai Import IDW + Import JSON

### Đã làm

**Triển khai 2 tính năng mới cho FittingManagement module:**

1. **Import IDW (Inventor COM Interop)** — `Services/FittingManagement/Import/FittingManagementService.IdwImport.cs` (MỚI)
   - Dùng late-binding COM (`Marshal.GetActiveObject` / `Activator.CreateInstance`) để kết nối Inventor
   - Trích xuất iProperties: PartNumber, Description, Revision, Mass, Material, Designer, Title
   - Duyệt Sheets → DrawingViews → ViewMetadata
   - Export DWG qua Inventor DWG Translator Add-In (GUID: `{C24E3AC2-122E-11D5-8E91-0010B541CD80}`)
   - Lưu FittingMetadata ra JSON vào `C:\Temp_BIM_Library`

2. **Import JSON + Tạo Block + Catalog** — `Services/FittingManagement/Import/FittingManagementService.JsonImport.cs` (MỚI)
   - Đọc JSON → FittingMetadata, tìm DWG cùng tên
   - Tạo block definition qua `Database.Insert()` từ side database
   - Inject 7 attributes: PART_ID, DESC, MASS, MATERIAL, REVISION, BOM_TYPE, POS_NUM
   - Map layer: PANEL → `MCG_Fitting_Panel` (blue), DETAIL → `MCG_Fitting_Detail` (red)
   - Đăng ký vào MasterCatalog.json qua `MergeItemsToJson()`

**Files đã sửa:**
- `Services/FittingManagement/IFittingManagementService.cs` — Thêm 2 method signatures
- `Views/FittingManagement/FittingManagementView.xaml.cs` — Thay 2 stub MessageBox bằng OpenFileDialog + gọi service

**Files đã tạo:**
- `Services/FittingManagement/Import/FittingManagementService.IdwImport.cs`
- `Services/FittingManagement/Import/FittingManagementService.JsonImport.cs`

### Trạng thái

- **Phase:** 1 — Feature Implementation
- Build succeeded — 0 errors
- Step 1 (Import IDW) và Step 2 (Import JSON) đã hoạt động

### Bước tiếp theo

1. Test thực tế: Load plugin vào AutoCAD → MCG_Show → Fitting tab → Import .idw files (cần Inventor)
2. Test Import JSON: Chọn JSON + DWG pair → kiểm tra block + catalog

### Ghi chú API

- **Inventor COM late-binding**: Dùng `dynamic` + `Type.GetTypeFromProgID("Inventor.Application")` — không cần reference DLL, không sửa .csproj
- **DWG Translator Add-In GUID**: `{C24E3AC2-122E-11D5-8E91-0010B541CD80}` — chuẩn cho tất cả phiên bản Inventor
- **Inventor iProperties path**: `PropertySets["Design Tracking Properties"]` chứa PartNumber, Description, Mass, Material; `PropertySets["Inventor Summary Information"]` chứa Title
- **Mass property**: Inventor trả về `double`, cần convert `.ToString("F3")`
- **COM lifecycle**: Track `weStartedInventor` flag — chỉ `Quit()` nếu ta khởi tạo, tránh kill session đang chạy của user

---

## Session 2026-04-09

### Đã làm

**Fix toàn bộ build errors (4 root causes):**

1. **Fix x:Class namespace trong 5 XAML files** — `ShipAutoCadPlugin.UI.*` → `MCGCadPlugin.Views.FittingManagement.*`:
   - `Views/FittingManagement/BOM/BomPreviewWindow.xaml`
   - `Views/FittingManagement/Library/FittingLibraryWindow.xaml`
   - `Views/FittingManagement/Library/Accessory/AccessoryManagerWindow.xaml`
   - `Views/FittingManagement/Library/Accessory/NewAccessoryWindow.xaml`
   - `Views/FittingManagement/Library/VirtualItemWindow.xaml`

2. **Chuyển 4 placeholder View thành WPF UserControl** (tạo .xaml + .xaml.cs, xóa .cs cũ):
   - `Views/DetailDesign/DetailDesignView.xaml` + `.xaml.cs`
   - `Views/PanelData/PanelDataView.xaml` + `.xaml.cs`
   - `Views/TableOfContent/TableOfContentView.xaml` + `.xaml.cs`
   - `Views/Weight/WeightView.xaml` + `.xaml.cs`

3. **Fix ambiguous Exception trong PaletteManager.cs** — thêm `using Exception = System.Exception;`

4. **Xóa `RecalculateSize`** — property không tồn tại trên PaletteSet AutoCAD 2023

### Trạng thái

- **Phase:** 0 — Scaffold & Setup ✅ HOÀN THÀNH
- Build succeeded — 0 errors, 0 warnings
- Plugin sẵn sàng load vào AutoCAD

### Bước tiếp theo

1. Test load plugin vào AutoCAD 2023 — chạy lệnh `MCG_Show`
2. Bắt đầu Phase 1 — triển khai logic cho từng Module

### Ghi chú API

- **x:Class phải khớp namespace code-behind** — nếu XAML dùng `ShipAutoCadPlugin.UI.X` mà code-behind dùng `MCGCadPlugin.Views.Y.X` thì WPF không generate partial class, gây lỗi `InitializeComponent` và tất cả control names
- **`Autodesk.AutoCAD.Runtime.Exception` xung đột với `System.Exception`** — khi `using Autodesk.AutoCAD.Runtime`, cần disambiguate bằng `using Exception = System.Exception;`
- **`PaletteSet.RecalculateSize` không có trong AutoCAD 2023 .NET API** — property này không tồn tại, xóa bỏ

---

## Session 2026-04-08

### Đã làm

**File đã sửa:**
- `CLAUDE.md` — Thêm bảng danh sách module (5 module), bổ sung pattern PaletteSet (thứ tự khởi tạo bắt buộc), thêm quy tắc `SetFocusToDwgView()`, cập nhật GUID thật, đổi log message sang English (theo user chỉnh)
- `Commands/PaletteManager.cs` — Fix 4 lỗi audit: cú pháp KeepFocus, thứ tự khởi tạo, size 400x600, GUID thật

**File placeholder đã tạo (25 file):**
- `Commands/{Module}/{Module}Command.cs` — 5 file (DetailDesign, FittingManagement, PanelData, TableOfContent, Weight)
- `Models/{Module}/{Module}Model.cs` — 5 file
- `Services/{Module}/{Module}Service.cs` — 5 file
- `Views/{Module}/{Module}View.cs` — 5 file
- `Utilities/{Module}/{Module}Utility.cs` — 5 file (đổi từ Helper → Utility cho khớp folder name)

**Folder đã tạo:**
- 5 module folders trong mỗi layer (`Commands/`, `Models/`, `Services/`, `Views/`, `Utilities/`):
  - `FittingManagement`, `Weight`, `TableOfContent`, `DetailDesign`, `PanelData`
- Folder `Module1`, `Module2` (template gốc) vẫn còn — chưa xóa, chờ user confirm

### Trạng thái

- **Phase:** 0 — Scaffold & Setup
- Cấu trúc folder + placeholder files đã xong, sẵn sàng push lên GitHub
- PaletteManager.cs đã fix xong audit
- Chưa có file `.xaml` nào — plugin chưa build được (5 View chưa chuyển sang UserControl)

### Vấn đề tồn đọng (từ Audit)

| # | Vấn đề | Trạng thái |
|---|---|---|
| #2.5 | Thiếu `[CommandMethod]` cho MCG_Show/Hide/Toggle | Chờ bổ sung sau |
| #6.2 | Chưa có 5 View + ViewModel | Chờ bổ sung sau |
| #3.3 | `Hide()` thiếu try/catch | Chờ bổ sung sau |
| #3.4 | `Toggle()` thiếu log | Chờ bổ sung sau |
| #3.5 | `Initialize()` thiếu try/catch | Chờ bổ sung sau |
| — | Folder `Module1`, `Module2` chưa xóa | Chờ user confirm |

### Bước tiếp theo

1. **Tạo 5 View (UserControl)** — để plugin build được:
   - `Views/DetailDesign/DetailDesignView.xaml` + `.xaml.cs`
   - `Views/FittingManagement/FittingManagementView.xaml` + `.xaml.cs`
   - `Views/PanelData/PanelDataView.xaml` + `.xaml.cs`
   - `Views/TableOfContent/TableOfContentView.xaml` + `.xaml.cs`
   - `Views/Weight/WeightView.xaml` + `.xaml.cs`
2. **Thêm `[CommandMethod]`** cho MCG_Show / MCG_Hide / MCG_Toggle trong PaletteManager.cs
3. **Tạo 5 ViewModel** tương ứng trong `Views/` hoặc folder riêng
4. **Thử build lần đầu** — `dotnet build -c Debug`

### Ghi chú API

- **PaletteSet thứ tự khởi tạo:** `new PaletteSet()` → `AddVisual()` → `DockEnabled/Size/KeepFocus` → `Visible = true`. Nếu set Size/Dock trước AddVisual, Palette sẽ không dock đúng.
- **KeepFocus = true:** Giữ focus trên palette sau click — cần kết hợp với `Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView()` trong button handler khi muốn trả focus về bản vẽ.
- **GUID cố định:** `2b80cfe9-c560-49d6-8a09-9d636260fcf2` — không được thay đổi sau khi deploy.
- **Log message:** User đã chỉnh quy tắc — log message viết bằng **English** (không phải tiếng Việt như ban đầu).
