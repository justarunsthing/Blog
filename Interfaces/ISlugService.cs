﻿namespace Blog.Interfaces
{
    public interface ISlugService
    {
        string UrlFriendly(string title);
        bool IsUnique(string slug);
    }
}