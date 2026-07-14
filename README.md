# RAG ChatBot - Ứng Dụng Học Tập Thông Minh

Ứng dụng web hỗ trợ học tập sử dụng kiến trúc RAG (Retrieval-Augmented Generation). Giảng viên tải tài liệu lên, AI tự động phân tích và tạo cơ sở tri thức. Sinh viên chat với AI để hỏi đáp dựa trên chính tài liệu của môn học.

---

## Công Nghệ Sử Dụng

| Thành phần | Công nghệ |
|---|---|
| Backend | ASP.NET Core MVC (.NET 9.0) |
| Database & Vector DB | PostgreSQL + pgvector (Supabase Cloud) |
| Lưu trữ file | Supabase Storage |
| Trích xuất PDF | UglyToad.PdfPig |
| AI Embedding & Chat | Ollama (Local AI) / Google Gemini / GitHub Models |
| Gửi email | Brevo (Sendinblue) |
| Xác thực | Cookie Auth + Google OAuth 2.0 |
| Mã hóa mật khẩu | BCrypt.Net-Next |

---

## Yêu Cầu Hệ Thống

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- Tài khoản [Supabase](https://supabase.com) (miễn phí)
  - PostgreSQL đã bật extension **pgvector**
  - Storage bucket tên `raw-documents`
- (Tùy chọn) Google Cloud Console project để dùng Google OAuth
- (Tùy chọn) Tài khoản [Brevo](https://brevo.com) để gửi email chào mừng
- AI API key (Google Gemini, GitHub Models) hoặc cài đặt **Docker** để chạy Local AI bằng **Ollama**.

---

## Cấu Hình `appsettings.json`

> **Vị trí:** `RAGChatBot.Presentation/appsettings.json`

File này bị liệt vào `.gitignore` nên **không được commit**. Bạn phải tự tạo file này trước khi chạy app.

Tham khảo file mẫu `appsettings.Example.json` trong cùng thư mục, sau đó sao chép và điền các giá trị thực:

```bash
# Từ thư mục gốc project
copy RAGChatBot.Presentation\appsettings.Example.json RAGChatBot.Presentation\appsettings.json
```

### Nội dung đầy đủ `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=YOUR_HOST.pooler.supabase.com;Database=postgres;Username=postgres.YOUR_PROJECT_REF;Password=YOUR_DB_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Supabase": {
    "Url": "https://YOUR_PROJECT_REF.supabase.co",
    "AnonKey": "YOUR_SUPABASE_ANON_KEY",
    "ServiceKey": "YOUR_SUPABASE_SERVICE_ROLE_KEY"
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    }
  },
  "AiSettings": {
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/openai",
    "ApiKey": "YOUR_AI_API_KEY",
    "EmbeddingModel": "text-embedding-004",
    "ChatModel": "gemini-2.0-flash"
  },
  "RagSettings": {
    "MaxCosineDistance": 0.55
  },
  "EmailSettings": {
    "ApiKey": "YOUR_BREVO_API_KEY",
    "SenderEmail": "no-reply@yourdomain.com",
    "SenderName": "RAG ChatBot Admin",
    "LoginUrl": "http://localhost:5178/Account/Login"
  },
  "PayOS": {
    "ClientId": "YOUR_PAYOS_CLIENT_ID",
    "ApiKey": "YOUR_PAYOS_API_KEY",
    "ChecksumKey": "YOUR_PAYOS_CHECKSUM_KEY",
    "ReturnUrl": "http://localhost:5178/Subscription/PaymentCallback",
    "CancelUrl": "http://localhost:5178/Subscription/PaymentCancelled"
  }
}
```

### Hướng dẫn lấy từng giá trị

#### 1. Supabase (Database + Storage)
1. Đăng ký tại [supabase.com](https://supabase.com) → tạo project mới
2. Vào **Project Settings → Database → Connection string** → chọn tab **URI** hoặc **Parameters** để lấy `Host`, `Username`, `Password`
3. Vào **Project Settings → API** để lấy `URL`, `anon public` key và `service_role` key. `ServiceKey` chỉ được dùng phía server, tuyệt đối không đưa vào client hoặc commit vào Git.
4. Vào **SQL Editor** → chạy lệnh sau để bật pgvector:
   ```sql
   CREATE EXTENSION IF NOT EXISTS vector;
   ```
5. Vào **Storage** → tạo bucket private tên **`raw-documents`**. File được tải xuống qua endpoint đã kiểm tra quyền của ứng dụng.

#### 2. AI API Key (Google Gemini)
1. Truy cập [aistudio.google.com/apikey](https://aistudio.google.com/apikey)
2. Tạo API key mới → điền vào `AiSettings:ApiKey`
3. `BaseUrl` để là `https://generativelanguage.googleapis.com/v1beta/openai`
4. `EmbeddingModel`: `text-embedding-004`, `ChatModel`: `gemini-2.0-flash`

> Hoặc dùng **9router**: `BaseUrl = https://aigw.9router.com/v1`, model `text-embedding-3-small`

#### 3. Google OAuth
1. Vào [console.cloud.google.com](https://console.cloud.google.com) → tạo project
2. **APIs & Services → Credentials → Create OAuth 2.0 Client ID**
3. Application type: **Web application**
4. Thêm Authorized redirect URI: `http://localhost:5178/signin-google`
5. Lấy `Client ID` và `Client Secret`

#### 4. Brevo Email (tùy chọn)
1. Đăng ký tại [brevo.com](https://brevo.com) → **SMTP & API → API Keys → Generate**
2. Điền API key vào `EmailSettings:ApiKey`
3. `SenderEmail` phải là email đã xác minh trong Brevo

#### 5. PayOS
1. Điền `ClientId`, `ApiKey` và `ChecksumKey` từ PayOS Dashboard.
2. `ReturnUrl` và `CancelUrl` phải là URL của ứng dụng.
3. Đăng ký webhook HTTPS công khai trỏ tới `https://YOUR_DOMAIN/api/payos/webhook`. Webhook là nguồn xác nhận thanh toán chính; return URL chỉ phục vụ trải nghiệm chuyển hướng của trình duyệt.

#### 6. Docker
`docker compose up --build` hiện chạy web app; PostgreSQL, Supabase Storage và AI API là dịch vụ bên ngoài. File `.dockerignore` ngăn cấu hình cục bộ và secrets bị đưa vào build context.

---

## Tính Năng Nổi Bật (Backend Core)
- **Kiến trúc RAG Thực thụ**: Kết hợp giữa CSDL Vector (pgvector) và sức mạnh của các LLMs.
- **Xử lý Ngầm Bền bỉ (Background Worker)**:
  - Tài liệu tải lên sẽ được đưa vào hàng đợi với 4 trạng thái rõ ràng: `Pending`, `Processing`, `Success`, và `Failed`.
  - Nếu API AI bị quá tải (Timeout), tài liệu sẽ chuyển sang trạng thái `Failed`. Giảng viên có thể bấm **Thử lại (Retry)** ngay trên giao diện mà không cần tải lại file.
- **Global Error Handling**: Middleware thông minh bắt toàn bộ Exception, tránh crash ứng dụng và trả về giao diện thân thiện cho người dùng cuối.
- **Chat UI Tương tác cao**: Hỗ trợ bôi đậm, in nghiêng, và định dạng Code (Markdown parser tích hợp) với hiệu ứng thị giác ấn tượng.

---

## Chạy Ứng Dụng

```bash
# 1. Clone repo
git clone <repo-url>
cd PRN222_AS1_G4

# 2. Tạo file cấu hình
copy RAGChatBot.Presentation\appsettings.Example.json RAGChatBot.Presentation\appsettings.json
# → Điền các giá trị thực vào file vừa tạo

# 3. Chạy app (tự động migrate DB khi khởi động)
cd RAGChatBot.Presentation
dotnet run
```

App sẽ chạy tại: **http://localhost:5178**

> Database migration chạy **tự động** và ứng dụng sẽ dừng nếu schema không thể cập nhật. Dữ liệu mẫu chỉ được seed trong Development hoặc khi đặt `SEED_DEMO_USERS=true`.

---

## Tài Khoản Mẫu (chỉ Development hoặc `SEED_DEMO_USERS=true`)

| Username | Mật khẩu | Vai trò | Gói |
|---|---|---|---|
| `lecturer_free` | Giá trị `SEED_ADMIN_PASSWORD`, hoặc mật khẩu ngẫu nhiên được in một lần khi seed | Lecturer | Free |
| `lecturer_premium` | Như trên | Lecturer | Premium |
| `admin` | Như trên | Admin | Premium |

---

## Cấu Trúc Dự Án

```
RAGChatBot.slnx
├── RAGChatBot.Domain/          # Entities, enums, shared models and contracts
├── RAGChatBot.BLL/             # Business logic, application services and DTOs
├── RAGChatBot.DAL/             # EF Core, repositories, AI, storage and email adapters
└── RAGChatBot.Presentation/    # ASP.NET Core Razor Pages, middleware and SignalR
    ├── appsettings.json        # ← Tạo file này (không commit)
    ├── appsettings.Example.json
    └── Program.cs
```

---

## Lưu Ý Bảo Mật

- **Không commit** `appsettings.json` lên Git (đã có trong `.gitignore`)
- Không chia sẻ API key, database password, Google OAuth secret
- Trên production, dùng biến môi trường hoặc Azure Key Vault thay cho `appsettings.json`
