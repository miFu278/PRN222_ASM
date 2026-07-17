# RAGChatBot Test Cases

Tài liệu này là checklist hồi quy cho toàn bộ tính năng chính của hệ thống. Các ca ghi `Unit` hoặc `E2E` đã có automated test; các ca `Manual` cần cấu hình dịch vụ ngoài.

Baseline hiện tại:

- 95 unit tests chạy độc lập, không gọi Internet.
- 11 integration/E2E tests dùng PostgreSQL `pgvector` qua Docker Testcontainers.
- Coverage unit hiện tại: BLL 51,31%, Domain 66,93%. DAL 4,44% vì phần lớn repository/migration được kiểm tra trong integration suite.

## Lệnh chạy

```powershell
# Unit tests, không cần Docker hay Internet
dotnet test RAGChatBot.Tests\RAGChatBot.Tests.csproj

# Integration/E2E, cần Docker đang chạy
dotnet test RAGChatBot.IntegrationTests\RAGChatBot.IntegrationTests.csproj

# Toàn bộ solution
dotnet test RAGChatBot.slnx
```

### Chạy E2E trên PostgreSQL thật

Tạo một database riêng có tên chứa `test` hoặc `e2e`, ví dụ `ragchatbot_test`. Database cần PostgreSQL 16, extension `vector`, và tài khoản kết nối phải có quyền chạy migration. Không dùng database production vì test sẽ thay đổi schema, tạo dữ liệu thử và thực hiện các ca xóa. Nên dùng bản clone có thể bỏ sau khi test.

```powershell
$env:RAGCHATBOT_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=ragchatbot_test;Username=postgres;Password=your-password"
dotnet test RAGChatBot.IntegrationTests\RAGChatBot.IntegrationTests.csproj --no-restore
Remove-Item Env:RAGCHATBOT_TEST_CONNECTION_STRING
```

Khi biến môi trường trên không được đặt, suite tự quay về PostgreSQL Testcontainer và cần Docker. Fixture sẽ từ chối connection string có tên database không chứa `test` hoặc `e2e` để tránh chạy nhầm trên production.

Thu coverage:

```powershell
dotnet test RAGChatBot.Tests\RAGChatBot.Tests.csproj --collect:"XPlat Code Coverage"
```

## 1. Tài khoản và phân quyền

| ID | Mức | Test case | Kết quả mong đợi |
|---|---|---|---|
| AUTH-01 | Unit | Đăng nhập đúng username/password | Trả về đúng user, role và subscription |
| AUTH-02 | Unit | Đăng nhập user không tồn tại | Trả null, không kiểm tra password hash |
| AUTH-03 | Unit | Đăng ký username mới | Hash password, trim họ tên, lưu đúng role |
| AUTH-04 | Unit | Đăng ký trùng username | Từ chối và không ghi database |
| AUTH-05 | E2E | Đăng nhập qua form | Phát cookie và truy cập được API bảo vệ |
| AUTH-06 | E2E | Gọi API khi chưa đăng nhập | HTTP 401, không trả HTML trang login |
| AUTH-07 | Unit | Xóa tài khoản Admin | Bị từ chối |
| AUTH-08 | Manual | Logout | Cookie bị xóa, truy cập trang bảo vệ chuyển về login |
| AUTH-09 | Manual | Google login hợp lệ | Tạo phiên đúng tài khoản đã whitelist |
| AUTH-10 | Manual | Google login email ngoài whitelist | Không tạo tài khoản/phiên |

## 2. Môn học và quyền Admin/Lecturer/Student

| ID | Mức | Test case | Kết quả mong đợi |
|---|---|---|---|
| COURSE-01 | Unit | Tạo môn không chọn giảng viên | Từ chối |
| COURSE-02 | Unit | Gán Admin/Student phụ trách môn | Từ chối, chỉ Lecturer hợp lệ |
| COURSE-03 | Unit | Tạo môn hợp lệ | Chuẩn hóa code, trim dữ liệu, phát sự kiện realtime |
| COURSE-04 | Unit | Đổi mã môn sau khi tạo | Từ chối vì liên kết document/quiz/chat |
| COURSE-05 | Unit | Sửa tên, mô tả, giảng viên | Cập nhật và phát `CourseUpdated` |
| COURSE-06 | Unit | Xóa môn | Xóa aggregate, dọn file và phát `CourseDeleted` |
| COURSE-07 | Unit | Storage lỗi khi dọn file | Môn vẫn được xóa, lỗi được log |
| COURSE-08 | E2E | Admin mở trang môn | Thấy Tạo/Sửa/Xóa, không thấy Document/Quiz |
| COURSE-09 | E2E | Admin gọi URL Document/Quiz trực tiếp | HTTP 403 |
| COURSE-10 | Manual | Lecturer chỉ thấy môn được phân công | Không thấy môn của lecturer khác |
| COURSE-11 | Manual | Student xem danh sách môn | Xem được danh sách nhưng không có nút quản trị |

## 3. Tài liệu và indexing

| ID | Mức | Test case | Kết quả mong đợi |
|---|---|---|---|
| DOC-01 | Unit | Upload định dạng không hỗ trợ | Từ chối trước khi ghi storage |
| DOC-02 | Unit | Lecturer không phụ trách upload | HTTP/service từ chối |
| DOC-03 | Unit | Admin upload dù đang được gán | Bị từ chối |
| DOC-04 | Unit | Free upload quá 5 MB | Bị từ chối |
| DOC-05 | Unit | Premium còn hạn upload dưới 50 MB | Thành công, tên file được sanitize |
| DOC-06 | Unit | Database lỗi sau khi lưu file | Xóa file orphan |
| DOC-07 | Unit | Lecturer phụ trách duyệt tài liệu | `IsApproved=true` |
| DOC-08 | Unit | Admin/Student duyệt tài liệu | Bị từ chối |
| DOC-09 | Unit | Lecturer cũ xóa tài liệu sau khi đổi người phụ trách | Bị từ chối |
| DOC-10 | Unit | Re-index môn | Chỉ queue tài liệu approved và Success/Failed |
| DOC-11 | Unit | Admin re-index | Bị từ chối |
| DOC-12 | Unit | Student tải tài liệu chưa approved/success | Bị từ chối |
| DOC-13 | Unit | Student tải tài liệu approved + success | Thành công |
| DOC-14 | Unit | Admin tải tài liệu | Bị từ chối |
| DOC-15 | E2E | Lecturer upload và download | Metadata + file private được lưu và đọc đúng |
| DOC-16 | Manual | Worker xử lý PDF/DOCX | Pending → Success, tạo chunks/vector |
| DOC-17 | Manual | Worker gặp file rỗng/API embedding lỗi | Chuyển Failed và cho phép Lecturer retry |

## 4. Chat RAG và quota

| ID | Mức | Test case | Kết quả mong đợi |
|---|---|---|---|
| CHAT-01 | Unit | Câu hỏi rỗng hoặc quá 4.000 ký tự | Từ chối trước khi gọi dependency |
| CHAT-02 | Unit | Thread thuộc user khác | Trả null/404 |
| CHAT-03 | Unit | Không chọn môn hoặc môn không tồn tại | Không trừ lượt |
| CHAT-04 | Unit | Free hết 10 lượt/ngày | Không gọi AI, trả `OutOfCredits` |
| CHAT-05 | Unit | Premium còn hạn | Có 50 lượt/ngày |
| CHAT-06 | Unit | Premium hết hạn | Quay về quota Free |
| CHAT-07 | Unit | AI thất bại | Hoàn lượt, không lưu exchange |
| CHAT-08 | Unit | AI thành công | Tạo thread, lưu 2 message và audit log |
| CHAT-09 | Unit | Không có chunk liên quan | Trả câu trả lời grounded, không gọi completion API |
| CHAT-10 | Unit | Có chunk liên quan | Gửi context/history, trả nguồn đúng document |
| CHAT-11 | E2E | Chat qua HTTP | Cookie → API → service → PostgreSQL, remaining giảm đúng |
| CHAT-12 | Manual | Hai request quota đồng thời | Không vượt giới hạn ngày |

## 5. Quiz

| ID | Mức | Test case | Kết quả mong đợi |
|---|---|---|---|
| QUIZ-01 | Unit | Bắt đầu quiz chưa publish | Bị từ chối |
| QUIZ-02 | Unit | Có attempt đang chạy | Trả lại attempt cũ, không tạo trùng |
| QUIZ-03 | Unit | Attempt cũ hết hạn | Đóng attempt cũ rồi tạo mới nếu còn lượt |
| QUIZ-04 | Unit | Password sai | Từ chối trước khi đọc quota attempt |
| QUIZ-05 | Unit | Bắt đầu hợp lệ | Snapshot câu hỏi, deadline và shuffle đúng cấu hình |
| QUIZ-06 | Unit | Nộp attempt của user khác | Bị từ chối |
| QUIZ-07 | Unit | Nộp attempt hết hạn | Đóng với điểm 0 rồi từ chối |
| QUIZ-08 | Unit | Đáp án trùng question | Dùng đáp án cuối, chuẩn hóa A/B/C/D |
| QUIZ-09 | E2E | Student start + submit qua HTTP | Lưu attempt/answer và tính điểm đúng |
| QUIZ-10 | Unit | AI trả JSON trong markdown fence | Parse thành question bank đúng |
| QUIZ-11 | Unit | AI trả 503 hoặc JSON lỗi | Trả danh sách rỗng, không crash worker |
| QUIZ-12 | Manual | Lecturer tạo/xóa quiz và câu hỏi | Thành công khi đang phụ trách môn |
| QUIZ-13 | E2E | Admin gọi Quiz UI/API | HTTP 403 |

## 6. Thanh toán và subscription

| ID | Mức | Test case | Kết quả mong đợi |
|---|---|---|---|
| PAY-01 | Unit | Tạo pending transaction | Lưu đúng orderId, amount, user |
| PAY-02 | Unit | User thanh toán không tồn tại | Từ chối, không ghi transaction |
| PAY-03 | Unit | Callback không hợp lệ | Không đổi trạng thái |
| PAY-04 | Unit | Callback thất bại/hủy | Transaction chuyển Failed |
| PAY-05 | Unit | Callback thành công đúng user và amount | Completed, nâng Premium và hạn sử dụng |
| PAY-06 | Unit | Webhook hợp lệ | Hoàn tất không phụ thuộc browser user |
| PAY-07 | Unit | Lịch sử giao dịch cá nhân | Chỉ trả transaction của user hiện tại |
| PAY-08 | Unit | Admin lọc status | Repository nhận đúng filter |
| PAY-09 | Manual | PayOS checkout thật | Redirect checkout URL và mô tả <= 9 ký tự |
| PAY-10 | Manual | Callback giả mạo orderCode/amount | Không nâng Premium |
| PAY-11 | Manual | Webhook gửi lặp | Idempotent, không gia hạn nhiều lần |

## 7. Whitelist, import và dashboard

| ID | Mức | Test case | Kết quả mong đợi |
|---|---|---|---|
| WL-01 | Unit | Email rỗng | Không query repository |
| WL-02 | Unit | Kiểm tra email hoa/thừa khoảng trắng | Chuẩn hóa lowercase + trim |
| WL-03 | Unit | Thêm email trùng | Từ chối, không gửi email |
| WL-04 | Unit | Thêm email hợp lệ | Lưu trước, sau đó gửi welcome email |
| WL-05 | Unit | Email provider lỗi | Entry vẫn được lưu |
| WL-06 | Unit | Xóa entry không tồn tại | Trả lỗi rõ ràng |
| WL-07 | Manual | Import Excel nhiều kiểu header | Nhận diện Email/Họ tên/MSSV, bỏ dòng lỗi và trùng |
| DASH-01 | Unit | Biểu đồ tháng | Luôn đủ 12 tháng, tháng thiếu bằng 0 |
| DASH-02 | Unit | Biểu đồ quý | Cộng đúng 3 tháng/quý |
| DASH-03 | Unit | Biểu đồ năm | Trả đúng cửa sổ 3 năm |
| DASH-04 | Manual | Admin lọc giao dịch theo trạng thái | Danh sách và tổng số khớp filter |

## 8. Adapter và file processing

| ID | Mức | Test case | Kết quả mong đợi |
|---|---|---|---|
| AI-01 | Unit | Embedding input rỗng | Trả vector rỗng, không gọi HTTP |
| AI-02 | Unit | Embedding hợp lệ 1.536 chiều | Trả đúng vector và prefix retrieval query |
| AI-03 | Unit | Embedding sai số chiều | Ném lỗi, không lưu vector lỗi |
| AI-04 | Unit | Batch embedding trả sai thứ tự | Sắp xếp lại theo `index` |
| FILE-01 | Unit | TextExtractor nhận stream null | `ArgumentNullException` |
| FILE-02 | Unit | Extension không hỗ trợ | `NotSupportedException` |
| FILE-03 | Unit | TXT/Markdown UTF-8 | Reset stream và đọc đủ nội dung |
| FILE-04 | Manual | PDF nhiều trang | Ghép nội dung theo thứ tự trang |
| FILE-05 | Manual | DOCX nhiều paragraph | Ghép đúng text trong `word/document.xml` |

## Smoke test trước khi demo

1. Login lần lượt bằng Admin, Lecturer và Student.
2. Admin tạo môn, sửa tên/mô tả, đổi giảng viên và xóa một môn thử nghiệm.
3. Xác nhận Admin không thấy và không truy cập được Document/Quiz.
4. Lecturer phụ trách upload PDF, chờ Success, duyệt tài liệu và tạo quiz.
5. Student tải tài liệu đã duyệt, chat đủ một câu, làm và nộp quiz.
6. Kiểm tra Free còn 9 lượt; Premium hiển thị giới hạn 50 lượt/ngày.
7. Tạo một giao dịch PayOS sandbox, kiểm tra lịch sử cá nhân và bộ lọc admin.
