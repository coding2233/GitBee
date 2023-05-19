//
// Created by EDY on 2023/5/19.
//

#include "imgui_git.h"

static bool libgit2_init_;
static  int index_test_;

void ShowGitCommit(git_repository *repo,const git_oid* oid)
{
    if(git_oid_is_zero(oid))
    {
        return;
    }

    git_commit* commit;
    git_commit_lookup(&commit,repo,oid);
    printf("commit %s",git_commit_message(commit));
    index_test_++;
    if (index_test_>10000)
    {
        git_commit_free(commit);
        return;
    }
    unsigned int count = git_commit_parentcount(commit);
    for (unsigned int i=0; i<count; i++) {
        auto *nth_parent_id = git_commit_parent_id(commit, i);
        if(git_oid_is_zero(nth_parent_id))
        {
            continue;
        }
        ShowGitCommit(repo,nth_parent_id);
    }
    git_commit_free(commit);
}

ImGuiGit::ImGuiGit(const char *git_path)
{
    //libgit2的初始化
    if (!libgit2_init_)
    {
        git_libgit2_init();
        libgit2_init_= true;
    }

    //"E:\\source\\HappyMahjongForDeveloper\\HappyMahjongForArtist"
    clock_t clock_start = clock();
    int error = git_repository_open(&repo_,git_path);
    if (error < 0) {
        const git_error *e = git_error_last();
        printf("Error %d/%d: %s\n", error, e->klass, e->message);
        exit(error);
    }
    else
    {
        clock_t clock_run_time = clock();
        long run_time = clock_run_time - clock_start;

        printf("open success! %ld %s",run_time,git_path);

        git_oid oid;
        git_oid_fromstr(&oid,"3e4d7be2b57b189b1cd0246e4f2b24ef953886e0");

        ShowGitCommit(repo_,&oid);

        clock_run_time = clock();
        run_time = clock_run_time - clock_start;

        printf("show commit success! %ld %s",run_time,git_path);

    }


}

ImGuiGit::~ImGuiGit()
{
    if(repo_)
    {
        git_repository_free(repo_);
    }
}
