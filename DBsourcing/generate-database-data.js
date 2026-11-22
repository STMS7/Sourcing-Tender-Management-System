const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const snapshotPath = path.join(root, 'Sourcing-Tender-Management-System', 'Sourcing-Tender-Management-System', 'Data', 'Migrations', 'ApplicationDbContextModelSnapshot.cs');
const source = fs.readFileSync(snapshotPath, 'utf8');

function entityBlocks(text) {
  const marker = 'modelBuilder.Entity("';
  const result = [];
  let cursor = 0;
  while ((cursor = text.indexOf(marker, cursor)) >= 0) {
    const nameStart = cursor + marker.length;
    const nameEnd = text.indexOf('"', nameStart);
    const braceStart = text.indexOf('{', nameEnd);
    if (braceStart < 0) break;
    let depth = 0;
    let quote = null;
    let escaped = false;
    let i = braceStart;
    for (; i < text.length; i++) {
      const ch = text[i];
      if (quote) {
        if (escaped) escaped = false;
        else if (ch === '\\') escaped = true;
        else if (ch === quote) quote = null;
        continue;
      }
      if (ch === '"' || ch === "'") { quote = ch; continue; }
      if (ch === '{') depth++;
      if (ch === '}' && --depth === 0) { i++; break; }
    }
    result.push({ entity: text.slice(nameStart, nameEnd), body: text.slice(braceStart + 1, i - 1) });
    cursor = i;
  }
  return result;
}

const blocks = entityBlocks(source);
const tables = new Map();
const entityToTable = new Map();

const groupRules = [
  ['identity', /^(AspNet|ApplicationUser|UserPasskey|Notification|UserTask)/],
  ['requests', /^(Departments|Categories|ProcurementRequest|WorkAssignment)/],
  ['tenders', /^(Tenders|Tender|EvaluationCriteria|WorkflowTransition|ProcessMessage|Approval)/],
  ['suppliers', /^(Suppliers|Supplier|TenderInvitation)/],
  ['bids', /^(Bids|Bid|Clarification|EvaluationReport|Committee)/],
  ['fulfilment', /^(Contracts|Contract|PurchaseOrder|Delivery)/],
  ['governance', /^(AuditLog|ContactMessage)/]
];

function groupFor(name) {
  return (groupRules.find(([, re]) => re.test(name)) || ['tenders'])[0];
}

function words(name) {
  return name.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^Asp Net /, 'ASP.NET ');
}

function describe(name) {
  const custom = {
    Tenders: 'Core sourcing event with its schedule, value, workflow status and selected award.',
    ProcurementRequests: 'Department purchasing requests that begin the sourcing workflow.',
    Suppliers: 'Registered supplier profiles, compliance details and account ownership.',
    Bids: 'Supplier offers submitted for a tender, including price and evaluation state.',
    BidAnalyses: 'Saved AI-assisted comparison and risk analysis for tender bids.',
    EvaluationReports: 'Committee recommendation submitted for management and final review.',
    CommitteeVotes: 'Top committee decisions and voting rationale for a tender.',
    WorkflowTransitions: 'Immutable history of tender status changes and responsible actors.',
    AuditLogs: 'Security and business audit trail for important system actions.',
    Contracts: 'Awarded supplier contract and commercial execution details.',
    PurchaseOrders: 'Orders raised from approved contracts.',
    Deliveries: 'Delivery records and acceptance status against purchase orders.'
  };
  return custom[name] || `${words(name)} records used by the sourcing and tender management workflow.`;
}

for (const block of blocks) {
  const tableMatch = block.body.match(/b\.ToTable\("([^"]+)"/);
  if (!tableMatch || !block.body.includes('b.Property<')) continue;
  const tableName = tableMatch[1];
  entityToTable.set(block.entity, tableName);
  if (tables.has(tableName)) continue;

  const columns = [];
  const propertyRe = /b\.Property<([^>]+)>\("([^"]+)"\)([\s\S]*?);/g;
  let match;
  while ((match = propertyRe.exec(block.body))) {
    const [, clrType, name, chain] = match;
    const dbType = (chain.match(/\.HasColumnType\("([^"]+)"\)/) || [])[1] || clrType;
    const required = chain.includes('.IsRequired()');
    const referenceType = /^(string|byte\[\])$/.test(clrType);
    const nullable = clrType.endsWith('?') || (referenceType && !required);
    const maxLength = (chain.match(/\.HasMaxLength\((\d+)\)/) || [])[1];
    const precision = chain.match(/\.HasPrecision\((\d+),\s*(\d+)\)/);
    const defaultSql = (chain.match(/\.HasDefaultValueSql\("([^"]+)"\)/) || [])[1];
    columns.push({
      name,
      type: precision ? `decimal(${precision[1]},${precision[2]})` : dbType,
      pk: false,
      fk: false,
      uq: false,
      null: nullable,
      ...(maxLength ? { maxLength: Number(maxLength) } : {}),
      ...(defaultSql ? { default: defaultSql } : {})
    });
  }

  const keyMatch = block.body.match(/b\.HasKey\(([^;]+?)\);/);
  const primaryKey = keyMatch ? [...keyMatch[1].matchAll(/"([^"]+)"/g)].map(x => x[1]) : [];
  primaryKey.forEach(name => { const col = columns.find(x => x.name === name); if (col) { col.pk = true; col.null = false; } });

  const indexes = [];
  const indexRe = /b\.HasIndex\(([^;]+?)\)([\s\S]*?);/g;
  while ((match = indexRe.exec(block.body))) {
    const indexColumns = [...match[1].matchAll(/"([^"]+)"/g)].map(x => x[1]);
    const chain = match[2];
    const unique = chain.includes('.IsUnique()');
    const indexName = (chain.match(/\.HasDatabaseName\("([^"]+)"\)/) || [])[1] || `IX_${tableName}_${indexColumns.join('_')}`;
    indexes.push({ name: indexName, columns: indexColumns, unique });
    if (unique && indexColumns.length === 1) {
      const col = columns.find(x => x.name === indexColumns[0]);
      if (col) col.uq = true;
    }
  }

  const identityRe = /UseIdentityColumn\(b\.Property<[^>]+>\("([^"]+)"\)\)/g;
  while ((match = identityRe.exec(block.body))) {
    const col = columns.find(x => x.name === match[1]);
    if (col) col.identity = true;
  }

  tables.set(tableName, {
    name: tableName,
    group: groupFor(tableName),
    description: describe(tableName),
    columns,
    primaryKey,
    primaryKeyName: `PK_${tableName}`,
    indexes
  });
}

const relationships = [];
const seenRelations = new Set();
for (const block of blocks) {
  const fromTable = entityToTable.get(block.entity);
  if (!fromTable || !block.body.includes('.HasOne(')) continue;
  const relationRe = /b\.HasOne\("([^"]+)"[\s\S]*?\.HasForeignKey\(([^;]+?)\)([\s\S]*?);/g;
  let match;
  while ((match = relationRe.exec(block.body))) {
    const toTable = entityToTable.get(match[1]);
    const fromColumns = [...match[2].matchAll(/"([^"]+)"/g)].map(x => x[1]);
    if (!toTable || !fromColumns.length) continue;
    const key = `${fromTable}|${fromColumns.join(',')}|${toTable}`;
    if (seenRelations.has(key)) continue;
    seenRelations.add(key);
    fromColumns.forEach(name => { const col = tables.get(fromTable)?.columns.find(x => x.name === name); if (col) col.fk = true; });
    const deleteBehavior = (match[3].match(/\.OnDelete\(DeleteBehavior\.([A-Za-z]+)\)/) || [])[1] || 'Cascade';
    relationships.push({
      name: `FK_${fromTable}_${toTable}_${fromColumns.join('_')}`,
      from: fromTable,
      fromColumns,
      to: toTable,
      toColumns: tables.get(toTable)?.primaryKey.slice(0, fromColumns.length) || ['Id'],
      deleteBehavior,
      required: !fromColumns.some(name => tables.get(fromTable)?.columns.find(x => x.name === name)?.null)
    });
  }
}

const tableList = [...tables.values()].sort((a, b) => a.name.localeCompare(b.name));
const design = {
  project: {
    name: 'Zaytouna Sourcing Portal',
    database: 'Sourcing Tender Management',
    engine: 'SQL Server',
    schema: 'dbo',
    description: 'A complete procurement workflow from department request and tender preparation through supplier bidding, committee evaluation, award, contracting, purchase orders and delivery.',
    stores: ['User accounts & roles', 'Department requests', 'Tenders & documents', 'Suppliers & invitations', 'Bids & evaluations', 'Awards & contracts', 'Audit & workflow history'],
    hubTable: 'Tenders',
    source: 'Generated from the current EF Core ApplicationDbContext model snapshot.',
    snapshotDate: new Date().toISOString().slice(0, 10),
    excludedTables: ['__EFMigrationsHistory']
  },
  groups: [
    { id: 'identity', name: 'Identity & user work' },
    { id: 'requests', name: 'Requests & catalog' },
    { id: 'tenders', name: 'Tender preparation' },
    { id: 'suppliers', name: 'Suppliers & invitations' },
    { id: 'bids', name: 'Bidding & evaluation' },
    { id: 'fulfilment', name: 'Contracts & fulfilment' },
    { id: 'governance', name: 'Governance & audit' }
  ],
  tables: tableList,
  relationships: relationships.sort((a, b) => a.from.localeCompare(b.from) || a.to.localeCompare(b.to)),
  rules: [
    'A tender is the central record connecting requests, items, documents, invitations, bids, committees, reports and the final award.',
    'Identity tables protect users, roles, claims, logins, tokens and passkeys independently from the procurement domain.',
    'Foreign keys enforce ownership and lifecycle links; delete behavior is shown for every relationship.',
    'WorkflowTransitions and AuditLogs preserve who changed important business state and when.',
    'BidAnalyses stores AI-assisted results. Final evaluation and award decisions remain human-controlled.',
    'The EF Core migration history table is infrastructure and is intentionally excluded from this documentation.'
  ]
};

const output = `/* Generated from ApplicationDbContextModelSnapshot.cs. Run node generate-database-data.js after schema migrations. */\n'use strict';\nglobalThis.DatabaseDesign = Object.freeze(${JSON.stringify(design, null, 2)});\n`;
fs.writeFileSync(path.join(__dirname, 'database-data.js'), output, 'utf8');
console.log(`Generated ${tableList.length} tables, ${relationships.length} relationships and ${tableList.reduce((n, t) => n + t.columns.length, 0)} columns.`);
