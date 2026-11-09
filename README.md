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
- Send a message from the contact page. Mail goes out through MailKit over SMTP.

Images for blogs and posts are stored on the row. New accounts must confirm their email before they can sign in. The seeded administrator is already confirmed.

## Run it

You need the .NET 9 SDK and PostgreSQL.

Point `ConnectionStrings:DefaultConnection` at your server. The project uses Npgsql. From the repository root you can override the checked-in local string with a user secret:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=Blog;Username=postgres;Password=YOUR_PASSWORD"
```

Put SMTP settings in user secrets as well. Do not commit the mailbox password.

```bash
dotnet user-secrets set "MailSettings:Mail" "you@gmail.com"
dotnet user-secrets set "MailSettings:DisplayName" "justarunsthing"
dotnet user-secrets set "MailSettings:Password" "YOUR_APP_PASSWORD"
dotnet user-secrets set "MailSettings:Host" "smtp.gmail.com"
dotnet user-secrets set "MailSettings:Port" "587"
```

Start the site:

```bash
dotnet run --launch-profile https
```

Open `https://localhost:7244`.

Startup applies migrations and seeds roles and the administrator if the database is empty.

## Stack

- .NET 9, ASP.NET Core MVC, Razor, Identity
- Entity Framework Core, Npgsql, PostgreSQL
- MailKit
- X.PagedList
