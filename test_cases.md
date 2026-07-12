# Bộ Test Cases Hoàn Chỉnh - Dự Án RAGChatBot (Week 9: Final Project)

Tài liệu này cung cấp danh sách đầy đủ các Test Cases để kiểm thử chức năng của hệ thống, bao gồm các dòng nghiệp vụ chính (Mainflows), phân quyền, thanh toán VNPay, cấu hình chia chunk văn bản, đo lường hiệu năng và cập nhật real-time qua SignalR.

---

## 1. Phân Hệ Đăng Nhập & Phân Quyền (Authentication & Authorization)

| Test Case ID | Tên Test Case | Tiền điều kiện | Các bước thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- | :--- | :--- |
| **TC-AUTH-01** | Đăng nhập Admin thành công | Tài khoản Admin tồn tại (`admin`) | 1. Truy cập trang `/Account/Login`<br>2. Nhập username `admin` và mật khẩu thích hợp.<br>3. Nhấn "Đăng nhập". | Đăng nhập thành công, chuyển hướng sang trang quản trị `/Admin/Dashboard`. |
| **TC-AUTH-02** | Đăng nhập Giảng viên thành công | Tài khoản Giảng viên tồn tại (`lecturer_premium`) | 1. Truy cập trang `/Account/Login`<br>2. Nhập username và mật khẩu thích hợp.<br>3. Nhấn "Đăng nhập". | Đăng nhập thành công, chuyển hướng về trang chủ `/`. Thanh điều hướng hiển thị các mục quản lý môn học phù hợp. |
| **TC-AUTH-03** | Đăng nhập qua Google OAuth2 | Trình duyệt đã đăng nhập tài khoản Google | 1. Tại trang đăng nhập, nhấn "Đăng nhập bằng Google".<br>2. Chọn tài khoản Google của bạn. | Đăng nhập thành công. Tài khoản sinh viên được tạo tự động với gói mặc định Free nếu là lần đầu tiên đăng nhập. |
| **TC-AUTH-04** | Ngăn chặn truy cập trang Admin | Đang đăng nhập tài khoản Học viên hoặc Giảng viên | 1. Cố gắng nhập thủ công URL `/Admin/Dashboard` hoặc `/Admin/Users` trên thanh địa chỉ. | Trình duyệt chặn truy cập, hiển thị trang Access Denied (Mã lỗi 403 Forbidden). |

---

## 2. Phân Hệ ChatBot & Hạn Mức Lượt Hỏi (ChatBot & Credits Limit)

| Test Case ID | Tên Test Case | Tiền điều kiện | Các bước thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- | :--- | :--- |
| **TC-CHAT-01** | Gửi tin nhắn và phản hồi RAG | Đăng nhập tài khoản Học viên, môn học có tài liệu học liệu đã được vector hóa | 1. Truy cập môn học cụ thể.<br>2. Nhập câu hỏi liên quan đến bài giảng vào khung chat và nhấn gửi. | 1. Tin nhắn của người dùng hiển thị lên khung chat.<br>2. Hiệu ứng loading dạng Washi Ink hiển thị.<br>3. Bot phản hồi chính xác dựa trên tài liệu môn học, định dạng Markdown hoạt động đúng. |
| **TC-CHAT-02** | Tạo cuộc trò chuyện mới (Chat Thread) | Đăng nhập tài khoản Học viên | 1. Nhấn nút "Cuộc trò chuyện mới" ở menu lịch sử chat. | Danh sách lịch sử hiển thị cuộc trò chuyện mới, màn hình chat chính được dọn sạch và sẵn sàng cho câu hỏi đầu tiên. |
| **TC-CHAT-03** | Khấu trừ Credit của tài khoản Free | Đăng nhập tài khoản Free (có 10 credits ban đầu) | 1. Nhìn vào bộ đếm credit hiển thị ở góc khung chat.<br>2. Tiến hành gửi 1 câu hỏi cho AI chatbot. | Bộ đếm giảm từ `10` xuống `9`. Tin nhắn gửi thành công và nhận phản hồi bình thường. |
| **TC-CHAT-04** | Từ chối phản hồi khi hết Credit (Free) | Đăng nhập tài khoản Free đã sử dụng hết 10 credits (còn 0) | 1. Cố gắng nhập câu hỏi mới và nhấn gửi. | 1. Hệ thống không trừ thêm credit.<br>2. Bot trả về thông báo lỗi: *"Bạn đã hết lượt hỏi miễn phí hôm nay... Nâng cấp Premium để chat không giới hạn!"* và kích hoạt cờ OutOfCredits để gợi ý nâng cấp. |
| **TC-CHAT-05** | Tài khoản Premium chat không giới hạn | Đăng nhập tài khoản Premium | 1. Bộ đếm hiển thị biểu tượng vô cực hoặc giá trị giả lập không giới hạn.<br>2. Thực hiện liên tục trên 10 câu hỏi chat. | Hệ thống không báo hết credit, cho phép chat liên tục không giới hạn. |

---

## 3. Phân Hệ Quản Lý Tài Liệu Học Liệu & Chiến Lược Chia Chunk (Manage Docs & Chunking)

| Test Case ID | Tên Test Case | Tiền điều kiện | Các bước thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- | :--- | :--- |
| **TC-DOC-01** | Tải lên tài liệu chiến lược Character | Đăng nhập tài khoản Giảng viên hoặc Admin, truy cập môn học | 1. Nhấn nút tải tài liệu.<br>2. Chọn file PDF/DOCX.<br>3. Chọn Chiến lược chia: **Character** (Ký tự). Thiết lập Size = 500, Overlap = 50.<br>4. Nhấn Upload. | Tài liệu được tải lên thành công, trạng thái hiển thị là "Pending...". Background worker xử lý chia chunk theo từng ký tự. |
| **TC-DOC-02** | Tải lên tài liệu chiến lược Word | Đăng nhập tài khoản Giảng viên hoặc Admin, truy cập môn học | 1. Nhấn nút tải tài liệu.<br>2. Chọn file PDF/DOCX.<br>3. Chọn Chiến lược chia: **Word** (Từ). Size = 200, Overlap = 20.<br>4. Nhấn Upload. | Tài liệu tải lên thành công. Background worker chia nhỏ văn bản dựa trên khoảng trắng và từ ngữ. |
| **TC-DOC-03** | Tải lên tài liệu chiến lược Paragraph | Đăng nhập tài khoản Giảng viên hoặc Admin, truy cập môn học | 1. Nhấn nút tải tài liệu.<br>2. Chọn file PDF/DOCX.<br>3. Chọn Chiến lược chia: **Paragraph** (Đoạn văn). Size = 1000.<br>4. Nhấn Upload. | Tài liệu tải lên thành công. Background worker chia nhỏ dựa theo ký tự xuống dòng kép (`\n\n`), giữ nguyên vẹn cấu trúc đoạn văn. |
| **TC-DOC-04** | Xử lý Background Worker & Vector hóa | Sau khi thực hiện tải tài liệu thành công ở các TC trên | 1. Chờ đợi trong giây lát và quan sát màn hình.<br>2. Nhấn nút Xem Chunks (nếu tài liệu đổi sang trạng thái thành công). | 1. Tài liệu đổi trạng thái từ "Pending" -> "Processing" -> "Vectorized" (Thành công).<br>2. Popup danh sách chunk hiển thị các đoạn nhỏ tương ứng với chiến lược chia đã chọn. |
| **TC-DOC-05** | Tự động tạo Quiz ôn tập | Tài liệu vừa được Vector hóa thành công | 1. Truy cập mục "Ôn luyện trắc nghiệm" của môn học tương ứng. | Hệ thống tự động trích xuất kiến thức từ tài liệu vừa tải lên và hiển thị một bộ câu hỏi trắc nghiệm tự động sinh để học viên tự luyện tập. |

---

## 4. Phân Hệ Báo Cáo & Thống Kê Doanh Thu (Report & Statistics)

| Test Case ID | Tên Test Case | Tiền điều kiện | Các bước thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- | :--- | :--- |
| **TC-REP-01** | Báo cáo doanh thu theo Tháng (Month) | Đăng nhập tài khoản Admin, truy cập Dashboard | 1. Tại bộ lọc thời gian, chọn chế độ "Month".<br>2. Chọn năm mong muốn kiểm tra. | Biểu đồ cột/đường hiển thị doanh thu chi tiết từ tháng 1 đến tháng 12 của năm đã chọn. |
| **TC-REP-02** | Báo cáo doanh thu theo Quý (Quarter) | Đăng nhập tài khoản Admin, truy cập Dashboard | 1. Tại bộ lọc thời gian, chọn chế độ "Quarter".<br>2. Chọn năm mong muốn kiểm tra. | Biểu đồ cột hiển thị doanh thu tổng hợp theo 4 cột: Quý 1, Quý 2, Quý 3, Quý 4 của năm đã chọn. |
| **TC-REP-03** | Báo cáo doanh thu theo Năm (Year) | Đăng nhập tài khoản Admin, truy cập Dashboard | 1. Tại bộ lọc thời gian, chọn chế độ "Year".<br>2. Chọn năm làm mốc so sánh. | Biểu đồ cột hiển thị tổng doanh thu của 3 năm liên tiếp (ví dụ: Năm hiện tại và 2 năm trước đó) để so sánh tăng trưởng. |

---

## 5. Phân Hệ Tích Hợp Thanh Toán VNPay (Subscriptions & Payments)

| Test Case ID | Tên Test Case | Tiền điều kiện | Các bước thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- | :--- | :--- |
| **TC-PAY-01** | Tạo liên kết thanh toán VNPay | Đăng nhập tài khoản Free, truy cập trang `/Subscription` | 1. Nhấn nút "Nâng cấp ngay" gói Premium.<br>2. Tại trang Checkout, xác nhận thông tin và bấm nút "Thanh Toán Qua VNPay". | Hệ thống tạo mã đơn hàng và chuyển hướng người dùng sang trang thanh toán thử nghiệm của cổng VNPay thành công. |
| **TC-PAY-02** | VNPay Callback giao dịch Thành Công | Đang ở trang thanh toán thử nghiệm của VNPay | 1. Chọn phương thức thanh toán ví dụ ứng dụng ngân hàng hoặc thẻ.<br>2. Nhập thông tin thẻ test của VNPay, nhập OTP giả định.<br>3. Hoàn tất thanh toán và chờ VNPay redirect ngược lại website của ứng dụng. | 1. Redirect về trang `/Subscription/PaymentCallback` hiển thị trạng thái: *"Thanh toán thành công!"*.<br>2. Gói cước của tài khoản được cập nhật ngay lập tức thành `Premium`. Ghi log giao dịch thành công. |
| **TC-PAY-03** | VNPay Callback giao dịch Thất Bại / Hủy | Đang ở trang thanh toán thử nghiệm của VNPay | 1. Nhấn nút "Hủy giao dịch" hoặc nhập sai mã OTP nhiều lần.<br>2. Chờ VNPay redirect ngược lại website của ứng dụng. | 1. Redirect về trang `/Subscription/PaymentCallback` hiển thị thông báo lỗi hoặc hủy thanh toán.<br>2. Gói cước tài khoản giữ nguyên là `Free`. Ghi log giao dịch với trạng thái thất bại. |

---

## 6. Phân Hệ Đo Lường Hiệu Năng (Metrics Benchmark)

| Test Case ID | Tên Test Case | Tiền điều kiện | Các bước thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- | :--- | :--- |
| **TC-BENCH-01** | Ghi nhận thời gian đo hiệu năng | Upload và xử lý một tài liệu học liệu mới | 1. Tiến hành tải lên 1 tài liệu để chạy ngầm.<br>2. Xem nhật ký log của hệ thống hoặc database. | Hệ thống đo chính xác thời gian thực thi của các tiến trình và tạo mới bản ghi trong bảng `PerformanceBenchmarks` với loại tác vụ tương ứng. |
| **TC-BENCH-02** | Hiển thị bảng số liệu Benchmark | Đăng nhập tài khoản Admin, truy cập Dashboard | 1. Kéo xuống phần "Tốc độ xử lý (Benchmark)". | Bảng dữ liệu hiển thị đúng thời gian xử lý trung bình của từng loại tác vụ (TextExtraction, Chunking, VectorEmbedding, QuizGeneration) tính bằng ms. |

---

## 7. Phân Hệ Đồng Bộ Môn Học Trực Tiếp qua SignalR (Real-time Updates)

| Test Case ID | Tên Test Case | Tiền điều kiện | Các bước thực hiện | Kết quả mong đợi |
| :--- | :--- | :--- | :--- | :--- |
| **TC-REAL-01** | Tự động đồng bộ khi thêm mới môn học | 1. Trình duyệt A đăng nhập Admin.<br>2. Trình duyệt B đăng nhập Học viên đang xem màn hình danh sách môn học `/Courses`. | 1. Tại Trình duyệt A, nhấn "Tạo Môn Mới".<br>2. Điền thông tin và bấm "Lưu thay đổi". | Môn học mới xuất hiện ngay lập tức tại Trình duyệt B mà không cần nhấn F5. Dòng môn học mới tải có hiệu ứng trượt GSAP. |
| **TC-REAL-02** | Tự động đồng bộ khi chỉnh sửa môn học | Như trên | 1. Tại Trình duyệt A, nhấn nút "Chỉnh sửa" của một môn học và thay đổi Tên hoặc Mô tả của môn đó.<br>2. Bấm "Lưu thay đổi". | Trình duyệt B tự động cập nhật tên môn học mới hoặc thông tin đã chỉnh sửa trực tiếp trên màn hình. |
| **TC-REAL-03** | Tự động đồng bộ khi xóa môn học | Như trên | 1. Tại Trình duyệt A, nhấn nút "Xóa bỏ" một môn học và xác nhận xóa. | Dòng môn học đó lập tức biến mất khỏi danh sách hiển thị trên Trình duyệt B. |
