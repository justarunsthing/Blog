using Blog.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Blog.Data
{
    public class ApplicationDbContext : IdentityDbContext<BlogUser>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Blog.Models.Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostAlias> PostAliases { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
        public DbSet<MailSend> MailSends { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Post>()
                .HasIndex(post => post.Slug)
                .IsUnique();

            builder.Entity<PostAlias>()
                .HasIndex(alias => alias.Slug)
                .IsUnique();

            builder.Entity<PostAlias>()
                .HasOne(alias => alias.Post)
                .WithMany()
                .HasForeignKey(alias => alias.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}