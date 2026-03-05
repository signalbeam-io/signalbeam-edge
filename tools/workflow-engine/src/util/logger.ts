import chalk from "chalk";

export type LogLevel = "debug" | "info" | "warn" | "error" | "step";

const PREFIXES: Record<LogLevel, string> = {
  debug: chalk.gray("DEBUG"),
  info: chalk.blue("INFO "),
  warn: chalk.yellow("WARN "),
  error: chalk.red("ERROR"),
  step: chalk.magenta("STEP "),
};

export const log = {
  debug(msg: string) {
    if (process.env.DEBUG) console.log(`${PREFIXES.debug} ${msg}`);
  },
  info(msg: string) {
    console.log(`${PREFIXES.info} ${msg}`);
  },
  warn(msg: string) {
    console.log(`${PREFIXES.warn} ${msg}`);
  },
  error(msg: string) {
    console.error(`${PREFIXES.error} ${msg}`);
  },
  step(id: string, status: string, duration?: number) {
    const dur = duration != null ? chalk.gray(` (${(duration / 1000).toFixed(1)}s)`) : "";
    const icon =
      status === "passed" ? chalk.green("PASS") :
      status === "failed" ? chalk.red("FAIL") :
      status === "skipped" ? chalk.gray("SKIP") :
      status === "running" ? chalk.cyan("RUN ") :
      chalk.gray("....");
    console.log(`${PREFIXES.step} [${icon}] ${id}${dur}`);
  },
  divider() {
    console.log(chalk.gray("─".repeat(60)));
  },
  header(title: string) {
    console.log("");
    console.log(chalk.bold.white(title));
    this.divider();
  },
};
