/* Buildless database documentation. All content comes from database-data.js. */
'use strict';
(function () {
  const data = globalThis.DatabaseDesign;
  if (!data) return;
  const $ = id => document.getElementById(id);
  const esc = value => String(value ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
  const groupMap = new Map(data.groups.map(group => [group.id, group]));
  const tableMap = new Map(data.tables.map(table => [table.name, table]));
  const outgoing = name => data.relationships.filter(link => link.from === name);
  const incoming = name => data.relationships.filter(link => link.to === name);
  const columnTotal = data.tables.reduce((sum, table) => sum + table.columns.length, 0);

  $('project-name').textContent = data.project.name;
  $('project-description').textContent = data.project.description;
  $('database-badge').textContent = `${data.project.engine} · ${data.project.database}`;
  $('stores').innerHTML = data.project.stores.map(item => `<span>${esc(item)}</span>`).join('');
  $('metrics').innerHTML = [[data.tables.length,'Tables'],[columnTotal,'Columns'],[data.relationships.length,'Relationships'],[data.groups.length,'Domains']].map(([number,label]) => `<div class="metric"><strong>${number}</strong><span>${label}</span></div>`).join('');
  $('schema-provenance').textContent = `${data.project.source} Snapshot ${data.project.snapshotDate}. ${data.project.excludedTables.join(', ')} is excluded; no records or private data are included.`;
  $('footer-context').textContent = `${data.project.name} / ${data.project.database} / ${data.project.schema}`;

  document.querySelectorAll('.tab').forEach(tab => tab.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(item => { item.classList.toggle('active', item === tab); item.setAttribute('aria-selected', item === tab); });
    document.querySelectorAll('.panel').forEach(panel => panel.hidden = panel.id !== `panel-${tab.dataset.panel}`);
    if (tab.dataset.panel === 'schema' && !schemaViewport.dataset.fitted) {
      schemaViewport.dataset.fitted = 'true';
      requestAnimationFrame(fitSchema);
    }
  }));

  function keyLabels(column) {
    return [column.pk && 'PK', column.fk && 'FK', column.uq && 'UQ'].filter(Boolean).map(key => `<span class="key">${key}</span>`).join('');
  }

  const schemaColors = ['#247985','#4f6fa8','#72509a','#ba692f','#3d846f','#9a557c','#65758b'];
  const schemaSvg = $('schema-content');
  const schemaViewport = $('schema-viewport');
  const schemaZoom = $('schema-zoom');
  const schemaBoxes = new Map();
  const cardWidth = 294;
  const headerHeight = 34;
  const rowHeight = 19;
  const columnGap = 86;
  const cardGap = 34;
  const canvasMargin = 54;
  let schemaCanvasWidth = 0;
  let schemaCanvasHeight = 0;
  let selectedSchemaTable = null;

  function schemaPoint(tableName, columnName, side) {
    const box = schemaBoxes.get(tableName);
    if (!box) return { x: 0, y: 0 };
    const columnIndex = Math.max(0, box.table.columns.findIndex(column => column.name === columnName));
    return {
      x: side === 'right' ? box.x + cardWidth : box.x,
      y: box.y + headerHeight + (columnIndex * rowHeight) + (rowHeight / 2)
    };
  }

  function renderSchemaDiagram() {
    schemaBoxes.clear();
    let maxHeight = 0;
    data.groups.forEach((group, groupIndex) => {
      let y = canvasMargin + 30;
      const x = canvasMargin + groupIndex * (cardWidth + columnGap);
      data.tables.filter(table => table.group === group.id).forEach(table => {
        const height = headerHeight + table.columns.length * rowHeight + 7;
        schemaBoxes.set(table.name, { table, group, groupIndex, x, y, height });
        y += height + cardGap;
      });
      maxHeight = Math.max(maxHeight, y);
    });
    schemaCanvasWidth = canvasMargin * 2 + data.groups.length * cardWidth + (data.groups.length - 1) * columnGap;
    schemaCanvasHeight = maxHeight + canvasMargin;

    const marker = `<defs><marker id="schema-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="5" markerHeight="5" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="#8aa5ac"></path></marker></defs>`;
    const links = data.relationships.map(link => {
      const fromBox = schemaBoxes.get(link.from);
      const toBox = schemaBoxes.get(link.to);
      if (!fromBox || !toBox) return '';
      const goesRight = toBox.x >= fromBox.x;
      const from = schemaPoint(link.from, link.fromColumns[0], goesRight ? 'right' : 'left');
      const to = schemaPoint(link.to, link.toColumns[0], goesRight ? 'left' : 'right');
      const distance = Math.max(45, Math.abs(to.x - from.x) * .44);
      const control1 = from.x + (goesRight ? distance : -distance);
      const control2 = to.x - (goesRight ? distance : -distance);
      return `<path class="schema-link" data-from="${esc(link.from)}" data-to="${esc(link.to)}" d="M ${from.x} ${from.y} C ${control1} ${from.y}, ${control2} ${to.y}, ${to.x} ${to.y}" marker-end="url(#schema-arrow)"><title>${esc(link.from)}.${esc(link.fromColumns.join(', '))} → ${esc(link.to)}.${esc(link.toColumns.join(', '))}</title></path>`;
    }).join('');

    const groupLabels = data.groups.map((group, index) => `<text class="group-label" x="${canvasMargin + index * (cardWidth + columnGap)}" y="${canvasMargin}">${esc(group.name)}</text>`).join('');
    const nodes = [...schemaBoxes.values()].map(box => {
      const color = schemaColors[box.groupIndex % schemaColors.length];
      const rows = box.table.columns.map((column, index) => {
        const y = headerHeight + index * rowHeight;
        const keys = [column.pk && 'PK', column.fk && 'FK', column.uq && 'UQ'].filter(Boolean).join('·');
        const nameX = keys ? 41 : 13;
        return `<g><rect class="column-row${index % 2 ? ' alt' : ''}" x="1" y="${y}" width="${cardWidth - 2}" height="${rowHeight}"></rect>${keys ? `<text class="column-key" x="9" y="${y + 13}">${keys}</text>` : ''}<text class="column-name" x="${nameX}" y="${y + 13}">${esc(column.name)}</text><text class="column-null" x="${cardWidth - 95}" y="${y + 13}">${column.null ? '?' : ''}</text><text class="column-type" x="${cardWidth - 10}" y="${y + 13}">${esc(column.type)}${column.identity ? ' ID' : ''}</text></g>`;
      }).join('');
      return `<g class="schema-node" data-table="${esc(box.table.name)}" transform="translate(${box.x} ${box.y})" role="button" tabindex="0" aria-label="${esc(box.table.name)}, ${box.table.columns.length} columns"><rect class="table-outline" width="${cardWidth}" height="${box.height}" rx="5"></rect><path d="M 5 0 H ${cardWidth - 5} Q ${cardWidth} 0 ${cardWidth} 5 V ${headerHeight} H 0 V 5 Q 0 0 5 0" fill="${color}"></path><text class="table-title" x="12" y="22">${esc(box.table.name)}</text><text class="table-count" x="${cardWidth - 10}" y="21">${box.table.columns.length} columns</text>${rows}<title>${esc(box.table.description)}</title></g>`;
    }).join('');

    schemaSvg.setAttribute('viewBox', `0 0 ${schemaCanvasWidth} ${schemaCanvasHeight}`);
    schemaSvg.innerHTML = `<title id="schema-svg-title">Zaytouna database schema</title><desc id="schema-svg-description">${data.tables.length} tables and ${data.relationships.length} foreign-key relationships grouped by business domain.</desc>${marker}<g class="relationships-layer">${links}</g><g class="tables-layer">${groupLabels}${nodes}</g>`;
    schemaSvg.querySelectorAll('.schema-node').forEach(node => {
      const activate = () => {
        selectedSchemaTable = selectedSchemaTable === node.dataset.table ? null : node.dataset.table;
        updateSchemaSelection();
      };
      node.addEventListener('click', activate);
      node.addEventListener('keydown', event => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          activate();
        }
      });
    });
    updateSchemaSelection();
  }

  function updateSchemaSelection() {
    const connected = selectedSchemaTable ? new Set([selectedSchemaTable, ...outgoing(selectedSchemaTable).map(link => link.to), ...incoming(selectedSchemaTable).map(link => link.from)]) : null;
    schemaSvg.querySelectorAll('.schema-node').forEach(node => {
      node.classList.toggle('selected', node.dataset.table === selectedSchemaTable);
      node.classList.toggle('dim', Boolean(connected && !connected.has(node.dataset.table)));
    });
    schemaSvg.querySelectorAll('.schema-link').forEach(link => {
      const active = Boolean(selectedSchemaTable && (link.dataset.from === selectedSchemaTable || link.dataset.to === selectedSchemaTable));
      link.classList.toggle('active', active);
      link.classList.toggle('dim', Boolean(selectedSchemaTable && !active));
    });
    $('schema-status').textContent = selectedSchemaTable
      ? `${selectedSchemaTable}: ${outgoing(selectedSchemaTable).length} outgoing and ${incoming(selectedSchemaTable).length} incoming relationships.`
      : 'Select a table to highlight its relationships.';
  }

  function applySchemaZoom(value) {
    const zoom = Math.max(20, Math.min(100, Number(value)));
    schemaZoom.value = String(Math.round(zoom / 5) * 5);
    schemaSvg.style.width = `${schemaCanvasWidth * zoom / 100}px`;
    schemaSvg.style.height = `${schemaCanvasHeight * zoom / 100}px`;
  }

  function fitSchema() {
    const available = Math.max(320, schemaViewport.clientWidth - 22);
    applySchemaZoom(Math.max(20, Math.min(100, available / schemaCanvasWidth * 100)));
    schemaViewport.scrollTo({ left: 0, top: 0, behavior: 'smooth' });
  }

  schemaZoom.addEventListener('input', event => applySchemaZoom(event.target.value));
  $('schema-zoom-out').addEventListener('click', () => applySchemaZoom(Number(schemaZoom.value) - 5));
  $('schema-zoom-in').addEventListener('click', () => applySchemaZoom(Number(schemaZoom.value) + 5));
  $('schema-fit').addEventListener('click', fitSchema);
  $('schema-legend').innerHTML = data.groups.map((group, index) => `<span><i style="background:${schemaColors[index % schemaColors.length]}"></i>${esc(group.name)}</span>`).join('');
  renderSchemaDiagram();
  applySchemaZoom(schemaZoom.value);

  let selectedTable = null;
  const filter = $('group-filter');
  data.groups.forEach(group => filter.insertAdjacentHTML('beforeend', `<option value="${esc(group.id)}">${esc(group.name)}</option>`));
  function renderDiagram() {
    const group = filter.value;
    const visible = data.tables.filter(table => group === 'all' || table.group === group);
    const connected = selectedTable ? new Set([selectedTable, ...outgoing(selectedTable).map(x=>x.to), ...incoming(selectedTable).map(x=>x.from)]) : null;
    $('er-content').innerHTML = visible.map(table => {
      const links = outgoing(table.name).length + incoming(table.name).length;
      const dim = connected && !connected.has(table.name) ? ' dim' : '';
      const selected = selectedTable === table.name ? ' selected' : '';
      return `<button class="entity${dim}${selected}" type="button" data-table="${esc(table.name)}"><span class="group-name">${esc(groupMap.get(table.group)?.name || table.group)}</span><strong>${esc(table.name)}</strong><small>${table.columns.length} columns · ${links} relationships</small></button>`;
    }).join('');
    $('er-content').querySelectorAll('.entity').forEach(button => button.addEventListener('click', () => { selectedTable = selectedTable === button.dataset.table ? null : button.dataset.table; renderDiagram(); }));
    $('relation-status').textContent = selectedTable ? `${selectedTable}: ${outgoing(selectedTable).length} outgoing, ${incoming(selectedTable).length} incoming` : `${visible.length} tables shown`;
  }
  filter.addEventListener('change', () => { selectedTable = null; renderDiagram(); });
  $('clear-selection').addEventListener('click', () => { selectedTable = null; renderDiagram(); });
  renderDiagram();

  function tableCard(table) {
    const references = outgoing(table.name).map(link => `${link.fromColumns.join(', ')} → ${link.to}`);
    const referencedBy = incoming(table.name).map(link => `${link.from} → ${link.toColumns.join(', ')}`);
    return `<article class="table-card" id="table-${esc(table.name)}" data-search="${esc((table.name+' '+table.description).toLowerCase())}"><header><strong>${esc(table.name)}</strong><p>${esc(table.description)}</p></header><div class="table-body"><table class="columns"><thead><tr><th>Column</th><th>SQL type</th><th>Keys</th><th>Required</th></tr></thead><tbody>${table.columns.map(col => `<tr><td>${esc(col.name)}</td><td>${esc(col.type)}${col.identity?' identity':''}</td><td>${keyLabels(col)||'—'}</td><td>${col.null?'No':'Yes'}</td></tr>`).join('')}</tbody></table><aside class="table-meta"><strong>Primary key</strong><p>${esc(table.primaryKey.join(', ') || 'None')}</p><strong>References</strong>${references.length?`<ul>${references.map(x=>`<li>${esc(x)}</li>`).join('')}</ul>`:'<p>None</p>'}<strong>Referenced by</strong>${referencedBy.length?`<ul>${referencedBy.map(x=>`<li>${esc(x)}</li>`).join('')}</ul>`:'<p>None</p>'}<strong>Indexes</strong><p>${table.indexes.length}</p></aside></div></article>`;
  }
  $('table-directory').innerHTML = data.tables.map(table => `<a href="#table-${esc(table.name)}">${esc(table.name)}</a>`).join('');
  $('tables-content').innerHTML = data.groups.map(group => {
    const members = data.tables.filter(table => table.group === group.id);
    return `<section class="group-section" data-group="${esc(group.id)}"><div class="group-title"><h3>${esc(group.name)}</h3><span>${members.length} tables</span></div>${members.map(tableCard).join('')}</section>`;
  }).join('');
  $('table-search').addEventListener('input', event => {
    const term = event.target.value.trim().toLowerCase();
    document.querySelectorAll('.table-card').forEach(card => card.hidden = term && !card.dataset.search.includes(term));
    document.querySelectorAll('.group-section').forEach(section => section.hidden = !section.querySelector('.table-card:not([hidden])'));
  });

  function quote(name) { return `[${String(name).replaceAll(']', ']]')}]`; }
  function createSql() {
    const lines = [`-- ${data.project.name}`, `-- Schema generated from EF Core model snapshot ${data.project.snapshotDate}.`, '-- Review the official EF Core migrations before production use.', '', `IF SCHEMA_ID(N'${data.project.schema}') IS NULL EXEC(N'CREATE SCHEMA ${quote(data.project.schema)}');`, ''];
    data.tables.forEach(table => {
      lines.push(`CREATE TABLE ${quote(data.project.schema)}.${quote(table.name)} (`);
      const definitions = table.columns.map(column => `    ${quote(column.name)} ${column.type}${column.identity?' IDENTITY(1,1)':''} ${column.null?'NULL':'NOT NULL'}${column.default?` DEFAULT ${column.default}`:''}`);
      if (table.primaryKey.length) definitions.push(`    CONSTRAINT ${quote(table.primaryKeyName)} PRIMARY KEY (${table.primaryKey.map(quote).join(', ')})`);
      lines.push(definitions.join(',\n'), ');', '');
    });
    data.relationships.forEach(link => lines.push(`ALTER TABLE ${quote(data.project.schema)}.${quote(link.from)} ADD CONSTRAINT ${quote(link.name)} FOREIGN KEY (${link.fromColumns.map(quote).join(', ')}) REFERENCES ${quote(data.project.schema)}.${quote(link.to)} (${link.toColumns.map(quote).join(', ')})${link.deleteBehavior==='Cascade'?' ON DELETE CASCADE':''};`));
    lines.push('');
    data.tables.flatMap(table => table.indexes.map(index => ({table,index}))).forEach(({table,index}) => lines.push(`CREATE ${index.unique?'UNIQUE ':''}INDEX ${quote(index.name)} ON ${quote(data.project.schema)}.${quote(table.name)} (${index.columns.map(quote).join(', ')});`));
    return lines.join('\n');
  }
  const sql = createSql();
  const filename = 'Sourcing-Tender-Management-current-schema.sql';
  $('sql-filename').textContent = filename;
  $('sql-code').textContent = sql;
  $('copy-sql').addEventListener('click', async () => { try { await navigator.clipboard.writeText(sql); $('sql-status').textContent='SQL copied to clipboard.'; } catch { $('sql-status').textContent='Copy was blocked. Select the script manually.'; } });
  $('download-sql').addEventListener('click', () => { const a=document.createElement('a'); a.href=URL.createObjectURL(new Blob([sql],{type:'text/sql'})); a.download=filename; a.click(); URL.revokeObjectURL(a.href); $('sql-status').textContent='SQL file downloaded.'; });

  const deleteCounts = data.relationships.reduce((acc, link) => { acc[link.deleteBehavior]=(acc[link.deleteBehavior]||0)+1; return acc; }, {});
  $('rules-content').innerHTML = `<div class="rules-grid">${data.rules.map((rule,i)=>`<article class="rule-card"><p class="eyebrow">Rule ${String(i+1).padStart(2,'0')}</p><p>${esc(rule)}</p></article>`).join('')}<article class="rule-card"><p class="eyebrow">Foreign key behavior</p><h3>Deletion policy</h3><ul>${Object.entries(deleteCounts).map(([name,count])=>`<li>${esc(name)}: ${count} relationships</li>`).join('')}</ul></article><article class="rule-card"><p class="eyebrow">Model coverage</p><h3>${data.tables.length} mapped tables</h3><p>${columnTotal} columns and ${data.relationships.length} foreign-key relationships are documented from the application snapshot.</p></article></div>`;
})();
