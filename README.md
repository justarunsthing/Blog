# Blog

An ASP.NET Core MVC blog. A blog is a subject. Posts live inside a blog. A post stays off the home page, the blog list, and search until it is Production.

## What you can do

- Read blogs and posts. The home page lists blogs that have at least one Production post, five per page.
- Open a post by its slug at `/BlogPosts/UrlFriendly/{slug}`. Production and Preview open for anyone with the link. Incomplete returns Not Found unless the signed-in user is the author or an administrator.
- Search Production posts by title, abstract, or body.
- Create a blog, with a name, description, and image. The create page requires the Administrator role.
- Write a post with an abstract, body, tags, an image, and a status of Incomplete, Preview, or Production.
- Comment on a post.
- Moderate a comment. Moderation stores a revised body, a reason, the time, and the signed-in user. Reasons include offensive language, hate speech, threatening speech, and the other values on `ModerationType`.
- Send a message from the contact page. Mail goes out through the Brevo HTTP API.

Images for blogs, posts, and users are stored on the row (`ImageData` and `ContentType`) and served from `/media/blogs/{id}`, `/media/posts/{id}`, and `/media/users/{id}`. New accounts must confirm their email before they can sign in. The seeded administrator is already confirmed.

The live site runs on Render at https://justarunsthing-tech-blog.onrender.com.

## Run it

You need the .NET 9 SDK and PostgreSQL.

The app reads `ConnectionStrings:DefaultConnection` through Npgsql. Do not commit a password. Locally, put the connection string in user secrets or in the environment. On Render, set `ConnectionStrings__DefaultConnection` to the Neon URL and include SSL (`sslmode=require`).

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=Blog;Username=postgres;Password=YOUR_PASSWORD"
```

`appsettings.json` keeps a local template with host, port, database, and username, and an empty password. The real string comes from user secrets or `ConnectionStrings__DefaultConnection`.

Startup applies EF Core migrations. It creates roles only when the database has no roles, and one administrator only when the database has no users. Set both values before that first boot. The environment variables are `Seed__Email` and `Seed__Password`. The config keys are `Seed:Email` and `Seed:Password`.

```bash
dotnet user-secrets set "Seed:Email" "you@example.com"
dotnet user-secrets set "Seed:Password" "YOUR_PASSWORD"
```

`Services/DataService.cs` creates every `BlogRole` (Administrator and Moderator) when the roles table is empty. When the users table is empty, it creates one user with the configured email and password, hardcodes the first name Arun and the last name Pun, marks the email confirmed, and adds that user to Administrator and Moderator.

Mail is Brevo. `EmailService` posts JSON to `https://api.brevo.com/v3/smtp/email` with the `api-key` header. Set `Brevo:ApiKey` (`Brevo__ApiKey` in the environment). Do not put the key in `appsettings.json`. The from address and the sender name are `MailSettings:Mail` and `MailSettings:DisplayName`.

```bash
dotnet user-secrets set "Brevo:ApiKey" "YOUR_BREVO_API_KEY"
```

Render sets `PORT`. `Program.cs` already listens on it.

Start the site:

```bash
dotnet run --launch-profile https
```

Open `https://localhost:7244`.

## Stack

- .NET 9, ASP.NET Core MVC, Razor, Identity
- Entity Framework Core, Npgsql, PostgreSQL
- Brevo HTTP API
- X.PagedList
