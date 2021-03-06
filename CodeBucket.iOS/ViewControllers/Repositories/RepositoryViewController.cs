﻿using System;
using CodeBucket.Core.ViewModels.Repositories;
using UIKit;
using CodeBucket.DialogElements;
using Humanizer;
using CodeBucket.Core.Utils;
using System.Collections.Generic;
using System.Reactive.Linq;
using ReactiveUI;
using Splat;
using CodeBucket.Core.Services;

namespace CodeBucket.ViewControllers.Repositories
{
    public class RepositoryViewController : PrettyDialogViewController<RepositoryViewModel>
    {
        private IDisposable _privateView;
        private readonly SplitButtonElement _split = new SplitButtonElement();
        private readonly SplitViewElement _split1 = new SplitViewElement(AtlassianIcon.Locked.ToImage(), AtlassianIcon.PageDefault.ToImage());
        private readonly SplitViewElement _split2 = new SplitViewElement(AtlassianIcon.Calendar.ToImage(), AtlassianIcon.Filezip.ToImage());
        private readonly SplitViewElement _split3 = new SplitViewElement(AtlassianIcon.Devtoolsrepository.ToImage(), AtlassianIcon.Flag.ToImage());

        private readonly ButtonElement _commitsButton = new ButtonElement("Commits", AtlassianIcon.Devtoolscommit.ToImage());
        private readonly ButtonElement _pullRequestsButton = new ButtonElement("Pull Requests", AtlassianIcon.Devtoolspullrequest.ToImage());
        private readonly ButtonElement _sourceButton = new ButtonElement("Source", AtlassianIcon.Filecode.ToImage());

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            HeaderView.SetImage(null, Images.RepoPlaceholder);

            var actionButton = new UIBarButtonItem(UIBarButtonSystemItem.Action) { Enabled = false };
            NavigationItem.RightBarButtonItem = actionButton;

            var watchers = _split.AddButton("Watchers", "-");
            var forks = _split.AddButton("Forks", "-");
            var branches = _split.AddButton("Branches", "-");

            _split3.Button2.Text = "- Issues";

            var featuresService = Locator.Current.GetService<IFeaturesService>();
 
            OnActivation(d => 
            {
                watchers.Clicked
                    .BindCommand(ViewModel.GoToStargazersCommand)
                    .AddTo(d);

                _commitsButton
                    .Clicked
                    .SelectUnit()
                    .BindCommand(ViewModel.GoToCommitsCommand)
                    .AddTo(d);

                _pullRequestsButton
                    .Clicked
                    .SelectUnit()
                    .BindCommand(ViewModel.GoToPullRequestsCommand)
                    .AddTo(d);

                _sourceButton
                    .Clicked
                    .SelectUnit()
                    .BindCommand(ViewModel.GoToSourceCommand)
                    .AddTo(d);

                branches.Clicked
                   .BindCommand(ViewModel.GoToBranchesCommand)
                   .AddTo(d);
                
                this.WhenAnyValue(x => x.ViewModel.BranchesCount)
                    .Subscribe(x => branches.Text = x.ToString())
                    .AddTo(d);
                
                this.WhenAnyValue(x => x.ViewModel.Watchers)
                    .Subscribe(x => watchers.Text = x?.ToString() ?? "-")
                    .AddTo(d);
                
                this.WhenAnyValue(x => x.ViewModel.Forks)
                    .Subscribe(x => forks.Text = x?.ToString() ?? "-")
                    .AddTo(d);

                this.WhenAnyValue(x => x.ViewModel.Repository).SelectUnit()
                    .Merge(this.WhenAnyValue(x => x.ViewModel.HasReadme).SelectUnit())
                    .Where(x => ViewModel.Repository != null)
                    .Subscribe(_ => Render())
                    .AddTo(d);

                this.WhenAnyValue(x => x.ViewModel.Issues)
                    .Select(x => "Issues".ToQuantity(x.GetValueOrDefault()))
                    .Subscribe(x => _split3.Button2.Text = x)
                    .AddTo(d);

                actionButton
                    .Bind(ViewModel.ShowMenuCommand)
                    .AddTo(d);

                this.WhenAnyValue(x => x.ViewModel.Repository)
                    .Where(x => x != null)
                    .Subscribe(x =>
                    {
                        if (x.IsPrivate && !featuresService.IsProEnabled)
                        {
                            if (_privateView == null)
                                _privateView = this.ShowPrivateView();
                            actionButton.Enabled = false;
                        }
                        else
                        {
                            actionButton.Enabled = true;
                            _privateView?.Dispose();
                        }
                    })
                    .AddTo(d);
            });
        }

        public void Render()
        {
            try
            {
                DoRender();
            }
            catch (Exception e)
            {
                RxApp.DefaultExceptionHandler.OnNext(e);
            }
        }

        private void DoRender()
        {
            var model = ViewModel.Repository;
            var avatarHref = ViewModel.Repository.Owner?.Links?.Avatar?.Href;
            var avatar = new Avatar(avatarHref).ToUrl(128);
            ICollection<Section> root = new LinkedList<Section>();
            HeaderView.SubText = string.IsNullOrWhiteSpace(model.Description) ? "Updated " + model.UpdatedOn.Humanize() : model.Description;
            HeaderView.SetImage(avatar, Images.RepoPlaceholder);
            RefreshHeaderView();

            var sec1 = new Section();

            _split1.Button1.Image = model.IsPrivate ? AtlassianIcon.Locked.ToImage() : AtlassianIcon.Unlocked.ToImage();
            _split1.Button1.Text = model.IsPrivate ? "Private" : "Public";
            _split1.Button2.Text = string.IsNullOrEmpty(model.Language) ? "N/A" : model.Language;
            sec1.Add(_split1);

            _split3.Button1.Text = model?.Scm?.ApplyCase(LetterCasing.Title) ?? "-";
            sec1.Add(_split3);

            _split2.Button1.Text = model.UpdatedOn.ToString("MM/dd/yy");
            _split2.Button2.Text = model.Size.Bytes().ToString("#.##");
            sec1.Add(_split2);

            var owner = new ButtonElement("Owner", model.Owner.Username) { Image = AtlassianIcon.User.ToImage() };
            owner.Clicked.SelectUnit().BindCommand(ViewModel.GoToOwnerCommand);
            sec1.Add(owner);

            if (model.Parent != null)
            {
                var parent = new ButtonElement("Forked From", model.Parent.Name) { Image = AtlassianIcon.Devtoolsfork.ToImage() };
                parent.Clicked.SelectUnit().BindCommand(ViewModel.GoToForkParentCommand);
                sec1.Add(parent);
            }

            var events = new ButtonElement("Events", AtlassianIcon.Blogroll.ToImage());
            events.Clicked.SelectUnit().BindCommand(ViewModel.GoToEventsCommand);
            var sec2 = new Section { events };

            if (model.HasWiki)
            {
                var wiki = new ButtonElement("Wiki", AtlassianIcon.Edit.ToImage());
                wiki.Clicked.SelectUnit().BindCommand(ViewModel.GoToWikiCommand);
                sec2.Add(wiki);
            }

            if (model.HasIssues)
            {
                var issues = new ButtonElement("Issues", AtlassianIcon.Flag.ToImage());
                issues.Clicked.SelectUnit().BindCommand(ViewModel.GoToIssuesCommand);
                sec2.Add(issues);
            }

            if (ViewModel.HasReadme)
            {
                var readme = new ButtonElement("Readme", AtlassianIcon.PageDefault.ToImage());
                readme.Clicked.SelectUnit().BindCommand(ViewModel.GoToReadmeCommand);
                sec2.Add(readme);
            }

            var sec3 = new Section { _commitsButton, _pullRequestsButton, _sourceButton };
            foreach (var s in new[] { new Section { _split }, sec1, sec2, sec3 })
                root.Add(s);

            if (!string.IsNullOrEmpty(ViewModel.Repository.Website))
            {
                var website = new ButtonElement("Website", AtlassianIcon.Weblink.ToImage());
                website.Clicked.SelectUnit().BindCommand(ViewModel.GoToWebsiteCommand);
                root.Add(new Section { website });
            }

            Root.Reset(root);
        }
    }
}