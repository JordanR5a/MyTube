using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml.Controls;

namespace MyTube.VideoLibrary
{
    public class PageNavigation
    {
        private static string Page_Type = "page";
        private static string Page_Args = "args";
        public Stack<Dictionary<string, Object>> Stack { get; set; }
        public Dictionary<string, Object> CurrentPage;
        private Type mainPage;

        public PageNavigation(Type mainPage)
        {
            this.mainPage = mainPage;
            Stack = new Stack<Dictionary<string, Object>>();
            CurrentPage = new Dictionary<string, object>
            {
                { Page_Type, mainPage},
                { Page_Args, null }
            };
        }

        public void Navigate(Frame frame, Type type)
        {
            Stack.Push(CurrentPage);
            CurrentPage = new Dictionary<string, object>
            {
                { Page_Type, type },
                { Page_Args, null }
            };
            frame.Navigate(type);
        }

        public void Navigate(Frame frame, Type type, Object args)
        {
            Stack.Push(CurrentPage);
            CurrentPage = new Dictionary<string, object>
            {
                { Page_Type, type },
                { Page_Args, args }
            };
            frame.Navigate(type, args);
        }

        public void Refresh(Frame frame)
        {
            frame.Navigate(CurrentPage[Page_Type] as Type, CurrentPage[Page_Args]);
        }

        public void Back(Frame frame)
        {
            CurrentPage = Stack.Pop();
            if (CurrentPage[Page_Args] != null) frame.Navigate(CurrentPage[Page_Type] as Type, CurrentPage[Page_Args]);
            else frame.Navigate(CurrentPage[Page_Type] as Type);
        }

        public void BackUntilPageChange(Frame frame)
        {
            var page = CurrentPage[Page_Type] as Type;
            do { App.PageNavigation.Back(frame); }
            while (App.PageNavigation.CurrentPage["page"] as Type == page);
        }

        public object MostRecentArgs(Type page)
        {
            return Stack.LastOrDefault(x => x[Page_Type] as Type == page)?[Page_Args];
        }

        public void Reset(Frame frame)
        {
            Navigate(frame, mainPage);
            Stack = new Stack<Dictionary<string, Object>>();
            CurrentPage = new Dictionary<string, object>
            {
                { Page_Type, mainPage},
                { Page_Args, null }
            };
        }
    }
}