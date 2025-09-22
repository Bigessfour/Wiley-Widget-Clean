#!/usr/bin/env node

/**
 * Custom Azure MCP Server - Workaround for @azure/mcp DI issues
 * Provides basic Azure CLI functionality through MCP protocol
 */

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import { spawn } from 'child_process';

// Azure CLI tools mapping
const AZURE_TOOLS = [
  {
    name: 'azure_resource_list',
    description: 'List Azure resources using az resource list',
    inputSchema: {
      type: 'object',
      properties: {
        resource_group: {
          type: 'string',
          description: 'Optional resource group name to filter by'
        },
        resource_type: {
          type: 'string',
          description: 'Optional resource type to filter by (e.g., Microsoft.Storage/storageAccounts)'
        }
      }
    }
  },
  {
    name: 'azure_resource_groups_list',
    description: 'List Azure resource groups using az group list',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  },
  {
    name: 'azure_storage_accounts_list',
    description: 'List Azure storage accounts using az storage account list',
    inputSchema: {
      type: 'object',
      properties: {
        resource_group: {
          type: 'string',
          description: 'Optional resource group name to filter by'
        }
      }
    }
  },
  {
    name: 'azure_vm_list',
    description: 'List Azure virtual machines using az vm list',
    inputSchema: {
      type: 'object',
      properties: {
        resource_group: {
          type: 'string',
          description: 'Optional resource group name to filter by'
        }
      }
    }
  },
  {
    name: 'azure_account_show',
    description: 'Show current Azure account information using az account show',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  }
];

class AzureMCPServer {
  constructor() {
    this.server = new Server(
      {
        name: 'azure-cli-mcp-server',
        version: '1.0.0',
      },
      {
        capabilities: {
          tools: {},
        },
      }
    );

    this.setupToolHandlers();
  }

  setupToolHandlers() {
    // List available tools
    this.server.setRequestHandler(ListToolsRequestSchema, async () => {
      return { tools: AZURE_TOOLS };
    });

    // Handle tool calls
    this.server.setRequestHandler(CallToolRequestSchema, async (request) => {
      const { name, arguments: args = {} } = request.params;

      try {
        switch (name) {
          case 'azure_resource_list':
            return await this.handleResourceList(args);
          case 'azure_resource_groups_list':
            return await this.handleResourceGroupsList();
          case 'azure_storage_accounts_list':
            return await this.handleStorageAccountsList(args);
          case 'azure_vm_list':
            return await this.handleVMList(args);
          case 'azure_account_show':
            return await this.handleAccountShow();
          default:
            throw new Error(`Unknown tool: ${name}`);
        }
      } catch (error) {
        return {
          content: [{ type: 'text', text: `Error: ${error.message}` }],
          isError: true
        };
      }
    });
  }

  async runAzureCommand(command, args = []) {
    return new Promise((resolve, reject) => {
      const az = spawn('az', [command, ...args], {
        stdio: ['pipe', 'pipe', 'pipe'],
        shell: true
      });

      let stdout = '';
      let stderr = '';

      az.stdout.on('data', (data) => {
        stdout += data.toString();
      });

      az.stderr.on('data', (data) => {
        stderr += data.toString();
      });

      az.on('close', (code) => {
        if (code === 0) {
          resolve(stdout.trim());
        } else {
          reject(new Error(`Azure CLI error (${code}): ${stderr || stdout}`));
        }
      });

      az.on('error', (error) => {
        reject(new Error(`Failed to execute Azure CLI: ${error.message}`));
      });
    });
  }

  async handleResourceList(args) {
    const commandArgs = ['resource', 'list', '--output', 'json'];

    if (args.resource_group) {
      commandArgs.push('--resource-group', args.resource_group);
    }

    if (args.resource_type) {
      commandArgs.push('--resource-type', args.resource_type);
    }

    const result = await this.runAzureCommand(commandArgs[0], commandArgs.slice(1));
    const resources = JSON.parse(result);

    return {
      content: [
        {
          type: 'text',
          text: `Found ${resources.length} Azure resources:\n\n${resources.map(r =>
            `- ${r.name} (${r.type}) in ${r.resourceGroup || 'N/A'}`
          ).join('\n')}`
        }
      ]
    };
  }

  async handleResourceGroupsList() {
    const result = await this.runAzureCommand('group', ['list', '--output', 'json']);
    const groups = JSON.parse(result);

    return {
      content: [
        {
          type: 'text',
          text: `Found ${groups.length} resource groups:\n\n${groups.map(g =>
            `- ${g.name} (${g.location})`
          ).join('\n')}`
        }
      ]
    };
  }

  async handleStorageAccountsList(args) {
    const commandArgs = ['storage', 'account', 'list', '--output', 'json'];

    if (args.resource_group) {
      commandArgs.push('--resource-group', args.resource_group);
    }

    const result = await this.runAzureCommand(commandArgs[0], commandArgs.slice(1));
    const accounts = JSON.parse(result);

    return {
      content: [
        {
          type: 'text',
          text: `Found ${accounts.length} storage accounts:\n\n${accounts.map(a =>
            `- ${a.name} (${a.kind}, ${a.location})`
          ).join('\n')}`
        }
      ]
    };
  }

  async handleVMList(args) {
    const commandArgs = ['vm', 'list', '--output', 'json'];

    if (args.resource_group) {
      commandArgs.push('--resource-group', args.resource_group);
    }

    const result = await this.runAzureCommand(commandArgs[0], commandArgs.slice(1));
    const vms = JSON.parse(result);

    return {
      content: [
        {
          type: 'text',
          text: `Found ${vms.length} virtual machines:\n\n${vms.map(vm =>
            `- ${vm.name} (${vm.hardwareProfile.vmSize}, ${vm.location}) - ${vm.powerState || 'Unknown state'}`
          ).join('\n')}`
        }
      ]
    };
  }

  async handleAccountShow() {
    const result = await this.runAzureCommand('account', ['show', '--output', 'json']);
    const account = JSON.parse(result);

    return {
      content: [
        {
          type: 'text',
          text: `Current Azure Account:\n- Name: ${account.name}\n- ID: ${account.id}\n- User: ${account.user?.name || 'N/A'}\n- Tenant: ${account.tenantId}\n- State: ${account.state}`
        }
      ]
    };
  }

  async start() {
    const transport = new StdioServerTransport();
    await this.server.connect(transport);
    console.error('Custom Azure MCP Server started successfully');
  }
}

// Start the server
const server = new AzureMCPServer();
server.start().catch((error) => {
  console.error('Failed to start server:', error);
  process.exit(1);
});