﻿using System;
using System.Linq;
using ReactiveUI;
using CodeBucket.Core.Services;
using System.Reactive.Linq;
using Splat;
using System.Reactive;
using CodeBucket.Client;
using System.Collections.Generic;

namespace CodeBucket.Core.ViewModels.Issues
{
    public class IssueAssigneeViewModel : ReactiveObject, ILoadableViewModel
    {
        private bool _isLoaded;

        public IReadOnlyReactiveList<IssueAssigneeItemViewModel> Assignees { get; }

        private string _selectedValue;
        public string SelectedValue
        {
            get { return _selectedValue; }
            set { this.RaiseAndSetIfChanged(ref _selectedValue, value); }
        }

        public ReactiveCommand<Unit, Unit> LoadCommand { get; }

        public ReactiveCommand<Unit, Unit> DismissCommand { get; } = ReactiveCommandFactory.Empty();

        public IssueAssigneeViewModel(
            string username, string repository,
            IApplicationService applicationService = null)
        {
            applicationService = applicationService ?? Locator.Current.GetService<IApplicationService>();

            var assignees = new ReactiveList<User>();
            Assignees = assignees.CreateDerivedCollection(CreateItemViewModel);

            this.WhenAnyValue(x => x.SelectedValue)
                .SelectMany(_ => Assignees)
                .Subscribe(x => x.IsSelected = string.Equals(x.Name, SelectedValue));
            
            LoadCommand = ReactiveCommand.CreateFromTask(async _ =>
            {
                if (_isLoaded) return;

                var repo = await applicationService.Client.Repositories.Get(username, repository);
                var users = new List<User>();

                try
                {
                    if (repo.Owner.Type == "team")
                    {
                        var members = await applicationService.Client.AllItems(x => x.Teams.GetMembers(username));
                        users.AddRange(members);
                    }
                    else
                    {
                        var privileges = await applicationService.Client.Privileges.GetRepositoryPrivileges(username, repository);
                        users.AddRange(privileges.Select(x => ConvertUserModel(x.User)));
                    }
                }
                catch (Exception e)
                {
                    this.Log().ErrorException("Unable to load privileges", e);
                }

                users.Add(repo.Owner);
                assignees.Reset(users.OrderBy(x => x.Username));
                _isLoaded = true;
            });
        }

        private static User ConvertUserModel(Client.V1.User user)
        {
            return new User
            {
                DisplayName = $"{user.FirstName} {user.LastName}",
                Username = user.Username,
                Links = new User.UserLinks
                {
                    Avatar = new Link(user.Avatar)
                }
            };
        }

        private IssueAssigneeItemViewModel CreateItemViewModel(User item)
        {
            var vm = new IssueAssigneeItemViewModel(item.Username, item.Links?.Avatar?.Href, string.Equals(SelectedValue, item.Username));
            vm.SelectCommand.Subscribe(y => SelectedValue = !vm.IsSelected ? vm.Name : null);
            vm.SelectCommand.BindCommand(DismissCommand);
            return vm;
        }
    }
}

