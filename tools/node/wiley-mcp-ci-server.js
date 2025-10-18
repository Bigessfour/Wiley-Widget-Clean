#!/usr/bin/env node

/**
 * Wiley-Widget CI MCP Server
 * Provides CI/CD diagnostic tools for Wiley-Widget project
 * Runs as HTTP server for CI integration
 */

import express from "express";
import cors from "cors";
import { execSync } from "child_process";
import { readFileSync, readdirSync } from "fs";
import { join, extname } from "path";

const app = express();
app.use(cors());
app.use(express.json());

// CI Tools mapping
const CI_TOOLS = [
  {
    name: "get_build_logs",
    description: "Get build logs for a specific commit SHA",
    inputSchema: {
      type: "object",
      properties: {
        sha: {
          type: "string",
          description: "Git commit SHA to get logs for",
        },
      },
      required: ["sha"],
    },
  },
  {
    name: "analyze_xaml",
    description: "Analyze XAML files for Syncfusion theme and control usage",
    inputSchema: {
      type: "object",
      properties: {
        path: {
          type: "string",
          description: "Path to XAML file or directory to analyze",
        },
      },
    },
  },
  {
    name: "check_syncfusion_license",
    description: "Check Syncfusion license status and configuration",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
];

// Routes
app.get("/health", (req, res) => {
  res.json({ status: "ok", server: "wiley-mcp-ci-server", tools: CI_TOOLS });
});

app.post("/tools/:toolName", async (req, res) => {
  const { toolName } = req.params;
  const args = req.body;

  try {
    let result;
    switch (toolName) {
      case "get_build_logs":
        result = await getBuildLogs(args.sha);
        break;
      case "analyze_xaml":
        result = await analyzeXaml(args.path || ".");
        break;
      case "check_syncfusion_license":
        result = await checkSyncfusionLicense();
        break;
      default:
        return res.status(404).json({ error: `Unknown tool: ${toolName}` });
    }
    res.json(result);
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

async function getBuildLogs(sha) {
  try {
    // Get git log for the specific SHA
    const logOutput = execSync(`git log --oneline -n 10 ${sha}`, {
      encoding: "utf8",
      cwd: process.cwd(),
    });

    // Try to find build-related files
    const buildFiles = [
      "build-errors.log",
      "logs/test-ui.log",
      "TestResults/**/*.trx",
    ];

    let buildContent = "";
    for (const pattern of buildFiles) {
      try {
        const files = execSync(`find . -name "${pattern.split('/').pop()}" -type f 2>/dev/null || true`, {
          encoding: "utf8",
          cwd: process.cwd(),
        });

        if (files.trim()) {
          const filePaths = files.trim().split('\n').filter(f => f);
          for (const filePath of filePaths.slice(0, 3)) { // Limit to 3 files
            try {
              const content = readFileSync(filePath.trim(), 'utf8').slice(0, 1000); // First 1000 chars
              buildContent += `\n--- ${filePath} ---\n${content}\n`;
            } catch (e) {
              // Ignore file read errors
            }
          }
        }
      } catch (e) {
        // Ignore find errors
      }
    }

    return {
      content: [
        {
          type: "text",
          text: `Build logs for SHA ${sha}:\n\nGit Log:\n${logOutput}\nBuild Files:\n${buildContent || "No build files found"}`,
        },
      ],
    };
  } catch (error) {
    return {
      content: [
        {
          type: "text",
          text: `Error getting build logs: ${error.message}`,
        },
      ],
    };
  }
}

async function analyzeXaml(path = ".") {
  try {
    const xamlFiles = findXamlFiles(path);
    let analysis = `Found ${xamlFiles.length} XAML files:\n\n`;

    for (const file of xamlFiles.slice(0, 5)) { // Limit analysis to 5 files
      try {
        const content = readFileSync(file, 'utf8');
        const syncfusionRefs = analyzeSyncfusionUsage(content);
        analysis += `File: ${file}\nSyncfusion Controls: ${syncfusionRefs.join(', ') || 'None'}\n\n`;
      } catch (e) {
        analysis += `File: ${file} - Error reading file\n\n`;
      }
    }

    return {
      content: [
        {
          type: "text",
          text: analysis,
        },
      ],
    };
  } catch (error) {
    return {
      content: [
        {
          type: "text",
          text: `Error analyzing XAML: ${error.message}`,
        },
      ],
    };
  }
}

async function checkSyncfusionLicense() {
  try {
    // Check for Syncfusion license file
    const licensePaths = [
      "SyncfusionLicense.txt",
      "licenses/SyncfusionLicense.txt",
      "secrets/SyncfusionLicense.txt",
    ];

    let licenseStatus = "License file not found in standard locations:\n";
    licenseStatus += licensePaths.join('\n');

    for (const path of licensePaths) {
      try {
        const content = readFileSync(path, 'utf8');
        if (content && content.length > 10) {
          licenseStatus = `License file found at ${path} (${content.length} characters)`;
          break;
        }
      } catch (e) {
        // Continue checking other paths
      }
    }

    return {
      content: [
        {
          type: "text",
          text: `Syncfusion License Status:\n${licenseStatus}`,
        },
      ],
    };
  } catch (error) {
    return {
      content: [
        {
          type: "text",
          text: `Error checking Syncfusion license: ${error.message}`,
        },
      ],
    };
  }
}

function findXamlFiles(startPath) {
  const xamlFiles = [];

  function traverse(dir) {
    try {
      const files = readdirSync(dir, { withFileTypes: true });
      for (const file of files) {
        const fullPath = join(dir, file.name);
        if (file.isDirectory() && !file.name.startsWith('.') && file.name !== 'node_modules') {
          traverse(fullPath);
        } else if (file.isFile() && extname(file.name).toLowerCase() === '.xaml') {
          xamlFiles.push(fullPath);
        }
      }
    } catch (e) {
      // Ignore directories we can't read
    }
  }

  traverse(startPath);
  return xamlFiles;
}

function analyzeSyncfusionUsage(content) {
  const syncfusionControls = [
    'Syncfusion',
    'SfDataGrid',
    'SfButton',
    'SfTextBox',
    'RibbonControlAdv',
    'DockingManager',
    'ChartControl',
  ];

  const found = [];
  for (const control of syncfusionControls) {
    if (content.includes(control)) {
      found.push(control);
    }
  }

  return found;
}

// Start the server
const port = process.env.PORT || 8080;
app.listen(port, () => {
  console.error(`Wiley MCP CI Server running on port ${port}`);
});