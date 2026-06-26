# 📋 Bảng Phân Chia Công Việc (Team 4 Người)

Dựa trên khối lượng công việc còn lại để dự án đạt chuẩn **Production Ready**, mình đã chia đều các task thành 4 nhóm (Roles) cân bằng nhau về độ khó và chuyên môn. Các bạn có thể thảo luận để nhận vai trò phù hợp với thế mạnh của từng người.

---

## 👨‍💻 Thành viên 1: Chuyên trách Real-time & Giao diện luồng (Real-time & Flow)
*Nhiệm vụ: Đảm bảo dữ liệu được cập nhật tức thời trên UI mà không cần tải lại trang.*

- [ ] **Thêm SignalR Hub**: Cấu hình `AddSignalR` và map Hub trong `Program.cs`.
- [ ] **Real-time Course Addition**: Gọi SignalR event khi Admin tạo mới môn học (trong Service/Controller).
- [ ] **UI SignalR Client**: Tích hợp `signalr.js` vào Razor Pages để lắng nghe sự kiện và tự động cập nhật danh sách môn học cho Giảng viên.
- [ ] **Real-time Document Status (Bonus)**: Cập nhật trạng thái xử lý tài liệu (Processing -> Success) realtime lên UI mà không cần F5.

---

## 🕵️‍♂️ Thành viên 2: Chuyên trách Bảo mật & Phân quyền (Security & Auth)
*Nhiệm vụ: Vá các lỗ hổng bảo mật và phân quyền truy cập chặt chẽ.*

- [ ] **Lọc môn học**: Sửa logic lấy danh sách môn học để Giảng viên chỉ xem được môn mình phụ trách (`SubjectLeaderId`).
- [ ] **Chặn học sinh xem Chunks**: Chặn truy cập endpoint trả về dữ liệu thô (Chunks) trong `Documents.cshtml.cs` đối với Role `Student`.
- [ ] **Rate Limiting (Chống Spam)**: Cấu hình `AspNetCore.RateLimiting` để chặn Học sinh gọi API AI Chat quá nhiều lần trong 1 phút.
- [ ] **Bảo mật Config**: Dọn dẹp mật khẩu Admin hardcode (`password123`) trong source code. Quản lý các Secret Keys (OpenAI, Supabase) an toàn bằng Environment Variables hoặc User Secrets.

---

## ⚙️ Thành viên 3: Chuyên trách Kiến trúc & Background Worker (Backend/System Core)
*Nhiệm vụ: Đảm bảo hệ thống chạy ngầm ổn định, không bị treo khi có lỗi từ AI API.*

- [ ] **Nâng cấp DocumentProcessingWorker**: Đổi logic `IsProcessed` thành các trạng thái: `Pending`, `Processing`, `Success`, `Failed`.
- [ ] **Cơ chế Retry (Thử lại)**: Bắt các Exception như Timeout của OpenAI và cho phép xử lý lại (Retry) các Document bị `Failed`.
- [ ] **Global Error Handling**: Thêm Middleware bắt mọi lỗi (Global Exception Handler) thay vì văng trang lỗi mặc định.
- [ ] **Tích hợp Serilog**: Cài đặt Serilog để lưu toàn bộ log (kể cả lỗi Background Worker) ra file `.txt` hoặc JSON, giúp truy vết lỗi dễ dàng khi sập Server.

---

## 🎨 Thành viên 4: Chuyên trách Trải nghiệm người dùng (UX) & Triển khai (DevOps)
*Nhiệm vụ: Chăm chút giao diện cuối cùng và chuẩn bị gói dự án để đưa lên Server.*

- [ ] **UI Loaders/Skeletons**: Thêm hiệu ứng Loading (Spinner/Skeleton) khi người dùng bấm Upload File hoặc khi đang chờ AI gõ câu trả lời (Typing indicator).
- [ ] **Mobile Responsive**: Rà soát lại CSS của trang Chat và trang Quản lý tài liệu để hiển thị đẹp trên màn hình điện thoại.
- [ ] **Viết Dockerfile**: Tạo `Dockerfile` để đóng gói toàn bộ ứng dụng ASP.NET Core Razor Pages.
- [ ] **Viết docker-compose.yml**: Đóng gói thêm PostgreSQL + PgVector vào Docker để quá trình cài đặt server Production chỉ mất 1 dòng lệnh (`docker-compose up -d`).

---

### 💡 Gợi ý quy trình làm việc chung (Workflow):
1. **Chia nhánh (Branching)**: Mỗi bạn nên tạo một branch riêng (VD: `feature/realtime`, `feature/security`) để tránh conflict code khi làm việc chung.
2. **Hỗ trợ chéo**: Phần của **Thành viên 1** và **Thành viên 4** khá sát với UI, trong khi **Thành viên 2** và **Thành viên 3** thiên về Backend/Database. Các bạn có thể pair-programming với nhau ở những điểm giao nhau.

Các bạn xem qua và điền tên vào bảng phân công nhé! Sau khi chọn xong, bạn muốn mình đóng vai (hỗ trợ) thành viên nào để code chức năng đó trước?
