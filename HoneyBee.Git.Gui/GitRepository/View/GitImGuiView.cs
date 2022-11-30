using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using SixLabors.ImageSharp.PixelFormats;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.GitRepository.View
{
    public class GitImGuiView : ImGuiView
    {
        public GitImGuiView(IContext context) : base(context)
        {
        }

        public override void OnDraw()
        {
        }

        public static void Push(Repository repository)
        {
            foreach (var branch in repository.Branches)
            {
                if (branch.IsCurrentRepositoryHead)
                {
                    //var signatureAuthor = repository.Config.BuildSignature(DateTimeOffset.Now);
                    LibGit2Sharp.PushOptions options = new LibGit2Sharp.PushOptions();
           
                    options.OnPushStatusError = (pushStatusErrors) => {
                        Log.Error("PushStatusError | Reference:{0} Message:{1}", pushStatusErrors.Reference, pushStatusErrors.Message);
                    };
                    //return false 为取消
                    options.OnPushTransferProgress = ( current,  total,  bytes) => {
                        Log.Info("OnPushTransferProgress | current number:{0} total number: {1} number of bytes: {2}", current, total, bytes);
                        return true; 
                    };
                    //return false 为取消
                    options.OnPackBuilderProgress = (stage,  current,  total) => {
                        Log.Info("OnPackBuilderProgress | stage :{0} current number:{1} total number: {2} ", stage, current, total);
                        return true; 
                    };
                    //return false 为取消
                    options.OnNegotiationCompletedBeforePush = (updates) => {
                        if (updates != null)
                        {
                            foreach (var item in updates)
                            { 
                                Log.Info("OnNegotiationCompletedBeforePush | SourceRefName :{0} DestinationRefName:{1} SourceObjectId: {2} DestinationObjectId: {3} ", item.SourceRefName, item.DestinationRefName, item.SourceObjectId, item.DestinationObjectId);
                            }
                        }
                        return true; 
                    };
                    repository.Network.Push(branch, options);
                    break;
                }
            }
        }

    }
}
