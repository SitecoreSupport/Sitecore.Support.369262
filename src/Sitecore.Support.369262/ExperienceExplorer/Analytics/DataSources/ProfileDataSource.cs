using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Abstractions;
using Sitecore.Analytics;
using Sitecore.Analytics.Data;
using Sitecore.Analytics.Tracking;
using Sitecore.Diagnostics;
using Sitecore.ExperienceExplorer.Analytics.DataSources;
using Sitecore.ExperienceExplorer.Core.Data.Profiles;
using Sitecore.Marketing.Definitions;
using Sitecore.Marketing.Definitions.Profiles;
using Sitecore.Marketing.Definitions.Profiles.Patterns;
using Sitecore.StringExtensions;
using ProfileData = Sitecore.Analytics.Model.ProfileData;

namespace Sitecore.Support.ExperienceExplorer.Analytics.DataSources
{
    [UsedImplicitly]
    public class ProfileDataSource : Sitecore.ExperienceExplorer.Analytics.DataSources.ProfileDataSource,
        IProfileDataSource
    {
        private readonly DefinitionManagerFactory _definitionManagerFactory;
        private readonly int _minimalProfileScoreCount;
        private readonly BaseTranslate _translate;

        public ProfileDataSource(
            BaseTranslate translate,
            DefinitionManagerFactory definitionManagerFactory) : base(translate, definitionManagerFactory)
        {
            Assert.ArgumentNotNull(translate, nameof(translate));
            Assert.ArgumentNotNull(definitionManagerFactory, nameof(definitionManagerFactory));

            _translate = translate;
            _definitionManagerFactory = definitionManagerFactory;
            _minimalProfileScoreCount = Profile.MinimalProfileScoreCount;
        }

        ICollection<ProfileViewerData> IProfileDataSource.GetViewerData()
        {
            var interaction = Tracker.Current.Interaction;
            if (interaction == null)
                return new List<ProfileViewerData>();
            var profileDefinitions = GetProfileDefinitions();
            var profileViewerDataList = new List<ProfileViewerData>();
            foreach (var trackedProfile in GetTrackedProfiles(interaction))
            {
                var profileDefinition1 = profileDefinitions[trackedProfile.ProfileName];
                if (profileDefinition1 != null && profileDefinition1.IsActive)
                {
                    var profileDefinition2 = CreateProfileDataFromProfileDefinition(profileDefinition1);
                    MergeProfileKeys(profileDefinition2, trackedProfile);
                    if (!profileDefinition2.ProfileKeys.All(x => Math.Abs(x.Value) < double.Epsilon))
                    {
                        var profileViewerData = new ProfileViewerData(profileDefinition2);
                        var matchedPatternCard = GetMatchedPatternCard(profileDefinition1, trackedProfile);
                        if (matchedPatternCard != null)
                            profileViewerData.PatternCardMatch = new PatternCardMatchData
                            {
                                Name = matchedPatternCard.Name,
                                Description = matchedPatternCard.Description,
                                MatchPatternText = BuildMatchPatternText(trackedProfile, matchedPatternCard.Name)
                            };
                        profileViewerDataList.Add(profileViewerData);
                    }
                }
            }

            return profileViewerDataList;
        }

        private IDefinitionCollection<IProfileDefinition> GetProfileDefinitions()
        {
            return Tracker.MarketingDefinitions.Profiles;
        }

        private IPatternCardDefinition GetMatchedPatternCard(
            IProfileDefinition profileDefinition,
            Profile trackedProfile)
        {
            var profileData =
                new ProfileData(profileDefinition.Name);
            foreach (var keyValuePair in trackedProfile)
                profileData.Values[keyValuePair.Key] = keyValuePair.Value;
            return _definitionManagerFactory.GetProfileDefinitionManager().MatchPattern(profileDefinition,
                GetMarketingDefinitionsProfileData(profileData, profileDefinition));
        }

        private IProfileData GetMarketingDefinitionsProfileData(
            ProfileData profileData,
            IProfileDefinition profileDefinition)
        {
            var profileData1 = new Marketing.Definitions.Profiles.Patterns.ProfileData
            {
                Count = profileData.Count,
                PatternId = profileData.PatternId,
                PatternLabel = profileData.PatternLabel,
                Total = profileData.Total,
                Values = new ProfileKeyValueDictionary()
            };
            foreach (var keyValuePair in profileData.Values)
            {
                var key = profileDefinition.GetKey(keyValuePair.Key);
                if (key != null)
                    profileData1.Values[key.Id] = keyValuePair.Value;
            }

            return profileData1;
        }

        private IEnumerable<Profile> GetTrackedProfiles(
            CurrentInteraction currentInteraction)
        {
            return currentInteraction.Profiles.GetProfileNames()
                .Select(profileName => currentInteraction.Profiles[profileName]);
        }

        private Sitecore.ExperienceExplorer.Core.Data.Profiles.ProfileData CreateProfileDataFromProfileDefinition(
            IProfileDefinition profileDefinition)
        {
            var list = profileDefinition.Keys.Select(profileKey => new ProfileKeyData
            {
                Key = profileKey.Name,
                Value = (double) profileKey.DefaultValue
            }).ToList();
            return new Sitecore.ExperienceExplorer.Core.Data.Profiles.ProfileData
            {
                Name = profileDefinition.Name.IsNullOrEmpty() ? profileDefinition.Alias : profileDefinition.Name,
                ProfileKeys = list
            };
        }

        private void MergeProfileKeys(
            Sitecore.ExperienceExplorer.Core.Data.Profiles.ProfileData target,
            IEnumerable<KeyValuePair<string, double>> profileKeys)
        {
            foreach (var profileKey1 in profileKeys)
            {
                var profileKey = profileKey1;
                var profileKeyData = target.ProfileKeys.Find(x =>
                    string.Equals(x.Key, profileKey.Key, StringComparison.InvariantCultureIgnoreCase));
                if (profileKeyData != null)
                    profileKeyData.Value = profileKey.Value;
            }
        }

        private string BuildMatchPatternText(Profile profile, string matchedPatternLabel)
        {
            if (!string.IsNullOrEmpty(profile.PatternLabel))
                return _translate.Text("<p>The visitor matches the pattern card <b>{0}</b>.</p>",
                    (object) profile.PatternLabel);
            var num = _minimalProfileScoreCount - profile.Count;
            return _translate.Text(
                "<p>Currently, this visitor matches the pattern card <b>{0}</b>.</p><p>The visitor has visited {1} page(s) that have profile cards assigned – personalization will be applied after {2} page(s).</p>",
                (object) matchedPatternLabel, (object) profile.Count, (object) num);
        }
    }
}