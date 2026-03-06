import type { GitInfo } from "../types.js";
import * as git from "../util/git.js";

export function buildGitInfo(repoRoot: string): GitInfo {
  return {
    branch: git.currentBranch(repoRoot),
    workingTreeClean: git.isCleanTree(repoRoot),
    headCommit: git.headCommit(repoRoot),
    hasCommitsVsDefault: git.hasCommitsVsDefault(repoRoot),
    changedFiles: git.changedFiles(repoRoot),
  };
}
