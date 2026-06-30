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
    "AnonKey": "YOUR_SUPABASE_ANON_KEY"
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
  "EmailSettings": {
    "ApiKey": "YOUR_BREVO_API_KEY",
    "SenderEmail": "no-reply@yourdomain.com",
    "SenderName": "RAG ChatBot Admin",
    "LoginUrl": "http://localhost:5178/Account/Login"
  }
}
```

### Hướng dẫn lấy từng giá trị

#### 1. Supabase (Database + Storage)
1. Đăng ký tại [supabase.com](https://supabase.com) → tạo project mới
2. Vào **Project Settings → Database → Connection string** → chọn tab **URI** hoặc **Parameters** để lấy `Host`, `Username`, `Password`
3. Vào **Project Settings → API** để lấy `URL` và `anon public` key
4. Vào **SQL Editor** → chạy lệnh sau để bật pgvector:
   ```sql
   CREATE EXTENSION IF NOT EXISTS vector;
   ```
5. Vào **Storage** → tạo bucket tên **`raw-documents`** (public hoặc private tùy cấu hình)

#### 2. AI API Key (Google Gemini)
1. Truy cập [aistudio.google.com/apikey](https://aistudio.google.com/apikey)
2. Tạo API key mới → điền vào `AiSettings:ApiKey`
3. `BaseUrl` để là `https://generativelanguage.googleapis.com/v1beta/openai`
4. `EmbeddingModel`: `text-embedding-004`, `ChatModel`: `gemini-2.0-flash`

> Hoặc dùng **9router**: `BaseUrl = https://aigw.9router.com/v1`, model `text-embedding-3-small`

#### 3. Google OAuth (tùy chọn)
1. Vào [console.cloud.google.com](https://console.cloud.google.com) → tạo project
2. **APIs & Services → Credentials → Create OAuth 2.0 Client ID**
3. Application type: **Web application**
4. Thêm Authorized redirect URI: `http://localhost:5178/signin-google`
5. Lấy `Client ID` và `Client Secret`

#### 4. Brevo Email (tùy chọn)
1. Đăng ký tại [brevo.com](https://brevo.com) → **SMTP & API → API Keys → Generate**
2. Điền API key vào `EmailSettings:ApiKey`
3. `SenderEmail` phải là email đã xác minh trong Brevo

#### 5. Ollama Local AI (Dành cho môi trường nội bộ)
Dự án đã tích hợp sẵn môi trường chạy AI cục bộ bằng Docker:
- Mở Terminal tại thư mục gốc của dự án.
- Chạy lệnh `docker-compose up -d` để tải về và chạy Engine **Ollama** kèm theo 2 models mặc định: `llama3.2:1b` (Chat) và `nomic-embed-text` (Embedding).
- Cập nhật `AiSettings:BaseUrl` thành `http://localhost:11434/v1` và cập nhật tên Model tương ứng.

> **Lưu ý với GPU Nvidia**: Container Ollama đã được cấu hình tự động nhận diện GPU Nvidia (`deploy: resources: reservations: devices: driver: nvidia`). Hãy đảm bảo bạn đã cài Docker Desktop hỗ trợ WSL2 hoặc Nvidia Container Toolkit.

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

> Database migration và seed dữ liệu mẫu chạy **tự động** khi khởi động lần đầu.

---

## Tài Khoản Mẫu (Seed tự động nếu DB trống)

| Username | Mật khẩu | Vai trò | Gói |
|---|---|---|---|
| `lecturer_free` | `password123` | Lecturer | Free |
| `lecturer_premium` | `password123` | Lecturer | Premium |
| `admin` | `password123` | Admin | Premium |

---

## Cấu Trúc Dự Án

```
RAGChatBot.sln
├── RAGChatBot.Domain/          # Entities, domain models
├── RAGChatBot.Application/     # Business logic, services, interfaces
├── RAGChatBot.Infrastructure/  # EF Core, Supabase, AI, Email
└── RAGChatBot.Presentation/    # ASP.NET MVC Controllers, Views
    ├── appsettings.json        # ← Tạo file này (không commit)
    ├── appsettings.Example.json
    └── Program.cs
```

---

## Lưu Ý Bảo Mật

- **Không commit** `appsettings.json` lên Git (đã có trong `.gitignore`)
- Không chia sẻ API key, database password, Google OAuth secret
- Trên production, dùng biến môi trường hoặc Azure Key Vault thay cho `appsettings.json`
