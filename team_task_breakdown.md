# 📋 Bảng Phân Chia Công Việc - Đồ Án Cuối Kỳ (Week 9 - Team 4 Người)

Tài liệu này phân chia chi tiết các đầu việc cho 4 thành viên để hoàn thiện **Đồ án cuối kỳ RAGChatBot** theo mô hình kiến trúc 3 lớp (3-Layers) và các yêu cầu nghiệp vụ chuyên sâu.

---

## 👨‍💻 Thành Viên 1: Backend Developer - Hệ Thống AI & Nghiệp Vụ
*Nhiệm vụ: Tập trung phát triển các logic nghiệp vụ AI cốt lõi (Chunking, Conversation Memory) và tự động sinh câu hỏi trắc nghiệm.*

*   **1. Cấu hình Chunking động (BLL & DAL)**:
    *   [x] Bổ sung trường cấu hình vào database (`KnowledgeDocument`: `ChunkingStrategy`, `ChunkSize`, `Overlap`).
    *   [x] Nâng cấp `ChunkingService.cs` hỗ trợ 3 chiến lược cắt đoạn: **Paragraph** (theo đoạn văn `\n\n`), **Word** (theo từ), và **Character** (theo ký tự).
    *   [x] Cập nhật `DocumentProcessingWorker.cs` để đọc đúng cấu hình chunking đã lưu trong database của từng file tài liệu khi xử lý nền.
*   **2. Ngân hàng câu hỏi & AI Quiz Generator (BLL & AI)**:
    *   [x] Tạo các thực thể database: `QuestionBank` (lưu trữ câu hỏi ôn tập) và `QuizAttempts` (lưu kết quả thi).
    *   [x] Viết service gọi API LLM (Gemini/OpenAI) đọc nội dung các chunks tài liệu để **tự động sinh ra bộ 5 - 10 câu hỏi trắc nghiệm** (kèm đáp án đúng).
*   **3. Bộ nhớ hội thoại Chatbot (BLL & AI)**:
    *   [x] Thiết lập lưu trữ tin nhắn chat vào database (`ChatMessages` liên kết với `ChatThreads`).
    *   [x] Nâng cấp `OpenAiChatService.cs` gửi kèm lịch sử tin nhắn trước đó (context) trong cuộc trò chuyện lên LLM để hỗ trợ chat liên tục nhiều lượt (Multi-turn Conversation).

---

## 🛡️ Thành Viên 2: Backend Developer - Thanh Toán, Credits & Bảo Mật
*Nhiệm vụ: Chịu trách nhiệm quản lý dòng tiền, giới hạn Credit cho thành viên và vá các lỗ hổng bảo mật.*
 
*   **1. Quản lý hạn mức Credit & Logs (BLL & DAL)**:
    *   [x] Bổ sung trường `DailyCredits` (mặc định 10) và `SubscriptionExpiresAt` vào thực thể `User`.
    *   [x] Tạo bảng ghi nhật ký sử dụng chatbot `ChatTrackerLogs` để lưu vết các lượt hỏi đáp.
    *   [x] Cập nhật logic `ChatApi.cshtml.cs` kiểm tra và khấu trừ 1 credit của sinh viên sau mỗi câu hỏi; ngăn sinh viên chat tiếp khi hết credit.
    *   [x] Viết background service tự động reset `DailyCredits = 10` cho toàn bộ sinh viên Free lúc **00:00 hàng ngày**.
*   **2. Tích hợp thanh toán PayOS (BLL & DAL)**:
    *   [x] Tạo bảng lưu trữ giao dịch nạp tiền `PaymentTransactions`.
    *   [x] Tích hợp SDK/API cổng thanh toán **VNPay** tạo link thanh toán QR động.
    *   [x] Xây dựng Webhook/Callback endpoint đón nhận dữ liệu giao dịch thành công từ VNPay ➡️ Nâng cấp gói cước thành `Premium` và ghi nhận log giao dịch.
*   **3. Phân quyền và Đối soát giao dịch (BLL & Presentation)**:
    *   [x] Thiết lập trang quản trị giao dịch thanh toán `/Admin/Payments` chỉ cho phép vai trò `Admin` truy cập để xem lịch sử nạp tiền của toàn hệ thống.

---

## 🎨 Thành Viên 3: Frontend Developer - Giao Diện Người Dùng & UX/UI
*Nhiệm vụ: Xây dựng các trang web giao diện cao cấp, trực quan và xử lý tương tác phía client (CSS, Razor Pages, Javascript).*

*   **1. UI Quản lý tài liệu & Cấu hình Chunking (Presentation)**:
    *   [ ] Nâng cấp trang `/Courses/Documents.cshtml`: Thêm biểu mẫu (Form) cho phép giảng viên tùy chọn cấu hình chunking (phương thức cắt, kích thước chunk, overlap) trước khi nhấn Upload.
*   **2. UI Làm bài tập trắc nghiệm & Kết quả Quiz (Presentation)**:
    *   [ ] Thiết kế giao diện sinh viên ôn luyện trực tuyến: hiển thị danh sách câu hỏi trắc nghiệm, thanh tiến trình thời gian đếm ngược, nút nộp bài và popup chấm điểm tự động.
*   **3. UI Chatbot thông minh & Chat History (Presentation)**:
    *   [ ] Tái thiết kế giao diện chat: Thêm thanh bên trái (Sidebar) hiển thị danh sách các phiên trò chuyện cũ (Chat Threads) như giao diện ChatGPT.
    *   [ ] Thêm khu vực hiển thị số credit còn lại trong ngày của sinh viên và hiệu ứng loading (Typing indicator/Skeleton) khi chờ phản hồi AI.
*   **4. UI Lịch sử giao dịch thanh toán (Presentation)**:
    *   [ ] Trang lịch sử thanh toán cá nhân cho sinh viên/giảng viên và trang quản lý đối soát giao dịch cho Admin.

---

## 📊 Thành Viên 4: Fullstack Developer - Thống Kê, Báo Cáo & Benchmarks
*Nhiệm vụ: Phát triển dashboard phân tích số liệu tài chính/hệ thống, đo lường benchmarks kỹ thuật và triển khai DevOps.*

*   **1. Dashboard Báo cáo & Thống kê doanh thu (BLL & Presentation)**:
    *   [ ] Xây dựng trang báo cáo hệ thống `/Admin/Dashboard` trực quan bằng biểu đồ (Chart.js).
    *   [ ] Triển khai các hàm tính toán doanh thu, số tài liệu xử lý, số lượt chat trong lớp BLL, hỗ trợ lọc theo **Tháng (Month)**, **Quý (Quarter)**, **Năm (Year)**.
*   **2. Technical Benchmarks & Đồ thị hiệu năng (DAL & Presentation)**:
    *   [ ] Tạo bảng `PerformanceBenchmarks` lưu trữ thời gian xử lý kỹ thuật.
    *   [ ] Đo lường thời gian (dùng `Stopwatch`): thời gian trích xuất text file, thời gian sinh vector embedding, thời gian LLM sinh phản hồi, tốc độ query cosine của PostgreSQL.
    *   [ ] Thiết kế giao diện đồ thị so sánh hiệu năng các chỉ số kỹ thuật trên trang Admin.
*   **3. Triển khai & Container hóa (DevOps)**:
    *   [x] Tối ưu hóa `Dockerfile` và `docker-compose.yml` để hệ thống tự động chạy Migration cơ sở dữ liệu và Seeding tài khoản mặc định ngay khi khởi chạy docker container.

---

### 💡 Quy tắc phối hợp chung (Workflow Rules)
1.  **Giao tiếp thông qua Interface**: Lớp `Presentation` và `BLL` chỉ tương tác thông qua các interface trong `BLL.Services` và `DAL.Interfaces`. Các thành viên Backend phải định nghĩa và tạo trước các file interface để Frontend có thể code mock dữ liệu mà không cần chờ Backend hoàn thiện.
2.  **Chia nhánh Git**: Mỗi thành viên làm việc trên nhánh riêng mang tên nhiệm vụ (Ví dụ: `feat/chunk-config`, `feat/payos-payment`).
3.  **Tích hợp sớm**: Thực hiện Merge code định kỳ mỗi 2-3 ngày về nhánh chung `refactor` hoặc `develop` để phát hiện xung đột sớm và chạy `dotnet build` xác thực tính tương thích.
