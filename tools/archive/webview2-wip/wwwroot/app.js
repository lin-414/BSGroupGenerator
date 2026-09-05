"use strict";

// ── 全局状态 ────────────────────────────────────────────────────────
let S = null;                        // 最近一次状态快照（C# 推送）
const checked = new Set();           // 勾选的服装名（待加入/移出）
const expandedMods = new Set();      // 手动展开的模组
const collapsedSeps = new Set();     // 手动收起的分隔符（默认展开）
let logLines = [];
let logTimer = null;

const $ = (id) => document.getElementById(id);
const send = (msg) => chrome.webview.postMessage(msg);
const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

// ── 消息入口 ────────────────────────────────────────────────────────
chrome.webview.addEventListener("message", (e) => handle(e.data));

function handle(m) {
  switch (m.type) {
    case "state":
      S = m;
      pruneChecked();
      renderAll();
      renderMembersIfOpen();
      break;
    case "log":
      pushLog(m.line);
      break;
    case "scanning":
      if (S) S.scanning = m.active;
      renderScanBadge();
      break;
    case "maximized":
      if (S) S.maximized = m.value;
      renderMaxGlyph();
      break;
    case "diagnostics":
      openDiag(m.text);
      break;
  }
}

document.addEventListener("DOMContentLoaded", () => {
  wireChrome();
  send({ type: "ready" });
});

// ── 静态事件接线 ────────────────────────────────────────────────────
function wireChrome() {
  document.querySelectorAll(".win-controls button").forEach((b) =>
    b.addEventListener("click", () => send({ type: "window", action: b.dataset.win })));

  $("titlebar").addEventListener("mousedown", (e) => {
    if (e.button === 0 && !e.target.closest("button")) send({ type: "drag" });
  });
  $("titlebar").addEventListener("dblclick", (e) => {
    if (!e.target.closest("button")) send({ type: "window", action: "max" });
  });

  $("btnRefreshInstances").onclick = () => send({ type: "refreshInstances" });
  $("btnAddMo2").onclick = () => send({ type: "addMo2Dir" });
  $("cboInstances").onchange = (e) => send({ type: "selectInstance", dir: e.target.value });
  $("cboProfiles").onchange = (e) => send({ type: "selectProfile", name: e.target.value });
  $("cboBodySlide").onchange = (e) => send({ type: "selectBodySlide", dir: e.target.value });
  $("btnDetectBs").onclick = () => send({ type: "detectBodySlide" });
  $("btnBrowseBs").onclick = () => send({ type: "browseBodySlide" });
  $("cboWriteMode").onchange = (e) => send({ type: "setWriteMode", mode: e.target.value });
  $("btnBrowseTarget").onclick = () => send({ type: "browseTargetDir" });

  $("btnNewGroup").onclick = () =>
    openInput("新建组", "", (v) => v && send({ type: "newGroup", name: v }));
  $("btnRenameGroup").onclick = () => {
    const g = currentGroup();
    if (!g) return tip("请先选中一个组。");
    openInput("重命名组", g.name, (v) => v && v !== g.name && send({ type: "renameGroup", old: g.name, new: v }));
  };
  $("btnDeleteGroup").onclick = () => {
    const g = currentGroup();
    if (!g) return tip("请先选中一个组。");
    if (confirm(`确定删除组「${g.name}」？（成员 ${g.count} 个）`))
      send({ type: "deleteGroup", name: g.name });
  };
  $("btnViewGroup").onclick = openMembers;
  $("btnImport").onclick = () => send({ type: "importGroups" });
  $("btnSave").onclick = () => send({ type: "save" });
  $("btnApplyAdd").onclick = () => apply(true);
  $("btnApplyRemove").onclick = () => apply(false);

  $("btnDiag").onclick = () => send({ type: "diagnostics" });
  $("btnLogToggle").onclick = toggleLog;
  $("diagClose").onclick = () => $("diagDialog").close();
  $("diagCopy").onclick = () => send({ type: "copyText", text: $("diagText").textContent });
  $("membersClose").onclick = () => $("membersDialog").close();
  $("membersRemove").onclick = removeCheckedMembers;
  $("membersFilter").addEventListener("input", renderMembers);

  $("txtFilter").addEventListener("input", debounce(renderTree, 200));
  $("chkUnassigned").onchange = renderTree;
  $("chkExpand").onchange = renderTree;

  document.addEventListener("keydown", (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "s") {
      e.preventDefault();
      send({ type: "save" });
    }
  });
}

// ── 数据辅助 ────────────────────────────────────────────────────────
const currentGroup = () => (S ? S.groups.find((g) => g.name === S.currentGroup) : null);
const inAnyGroup = (name) => S.groups.some((g) => g.members.includes(name));
const isMember = (name) => {
  const g = currentGroup();
  return g ? g.members.includes(name) : false;
};

function pruneChecked() {
  if (!S) return;
  const all = new Set();
  for (const sec of S.sections) for (const o of sec.outfits) all.add(o.name);
  for (const name of [...checked]) if (!all.has(name)) checked.delete(name);
}

function debounce(fn, ms) {
  let t = null;
  return (...args) => {
    clearTimeout(t);
    t = setTimeout(() => fn(...args), ms);
  };
}

function toggleSet(set, value) {
  set.has(value) ? set.delete(value) : set.add(value);
}

// ── 全量渲染 ────────────────────────────────────────────────────────
function renderAll() {
  renderTop();
  renderGroups();
  renderTree();
  renderStatus();
  renderMaxGlyph();
}

function renderTop() {
  fillSelect($("cboInstances"), S.instances.map((i) => [i.dir, i.display]), S.selectedInstance);
  fillSelect($("cboProfiles"), S.profiles.map((p) => [p, p]), S.selectedProfile);
  fillSelect($("cboBodySlide"), S.bsCandidates.map((c) => [c.dir, c.dir]), S.selectedBs);
  $("cboWriteMode").value = S.writeMode;
  $("infoLine").textContent = S.infoText + (S.dirty ? "　·　有未保存修改" : "") + (S.scanning ? "　·　扫描中…" : "");
}

function fillSelect(sel, pairs, selected) {
  sel.innerHTML = "";
  for (const [v, t] of pairs) {
    const o = document.createElement("option");
    o.value = v;
    o.textContent = t;
    sel.appendChild(o);
  }
  if (selected !== null && selected !== undefined && pairs.some(([v]) => v === selected))
    sel.value = selected;
  if (sel.value === "" && sel.options.length)
    sel.selectedIndex = 0;
}

// ── 左侧树 ──────────────────────────────────────────────────────────
function renderTree() {
  if (!S) return;
  const tree = $("tree");
  const filter = $("txtFilter").value.trim().toLowerCase();
  const unassignedOnly = $("chkUnassigned").checked;
  const autoExpand = $("chkExpand").checked;

  // 先计算可见段
  const display = [];
  for (const sec of S.sections) {
    const outfits = sec.outfits.filter((o) => {
      if (unassignedOnly && inAnyGroup(o.name)) return false;
      if (!filter) return true;
      return sec.owner.toLowerCase().includes(filter)
          || (sec.separator || "").toLowerCase().includes(filter)
          || o.name.toLowerCase().includes(filter);
    });
    if (outfits.length === 0) continue;
    display.push({ separator: sec.separator, owner: sec.owner, outfits });
  }

  const frag = document.createDocumentFragment();
  let currentSepNode = null;
  let currentSepName = null;
  let shownOutfits = 0;

  // 连续相同名称的分隔符共用同一个节点
  for (const sec of display) {
    if (currentSepName !== sec.separator || currentSepNode === null && sec.separator !== null) {
      currentSepName = sec.separator;
      currentSepNode = null;
      if (sec.separator !== null) {
        const row = rowDiv("trow sep");
        const twist = twistBtn(!collapsedSeps.has(sec.separator), () => {
          toggleSet(collapsedSeps, sec.separator);
          renderTree();
        });
        const label = spanEl("name", sec.separator);
        row.append(twist, label);
        frag.appendChild(row);
        currentSepNode = row;
      }
    }

    const expanded = autoExpand || expandedMods.has(sec.owner);
    const modRow = rowDiv("trow mod");
    const twist = twistBtn(expanded, () => {
      toggleSet(expandedMods, sec.owner);
      renderTree();
    });

    const box = document.createElement("input");
    box.type = "checkbox";
    const allChecked = sec.outfits.every((o) => checked.has(o.name));
    const someChecked = sec.outfits.some((o) => checked.has(o.name));
    box.checked = allChecked;
    box.indeterminate = !allChecked && someChecked;
    box.onclick = () => {
      sec.outfits.forEach((o) => (box.checked ? checked.add(o.name) : checked.delete(o.name)));
      renderTree();
    };

    const name = spanEl("name", sec.owner);
    name.onclick = () => {
      toggleSet(expandedMods, sec.owner);
      renderTree();
    };
    const count = spanEl("count", `(${sec.outfits.length})`);
    modRow.append(twist, box, name, count);

    const cg = currentGroup();
    if (cg) {
      const inGroup = sec.outfits.filter((o) => cg.members.includes(o.name)).length;
      if (inGroup > 0) {
        modRow.append(spanEl("ingroup", `[组内 ${inGroup}/${sec.outfits.length}]`));
        if (inGroup === sec.outfits.length) name.classList.add("member");
      }
    }

    if (currentSepNode !== null && currentSepName === sec.separator)
      currentSepNode.appendChild(modRow);
    else
      frag.appendChild(modRow);

    if (expanded) {
      for (const o of sec.outfits) {
        const orow = rowDiv("trow outfit");
        const obox = document.createElement("input");
        obox.type = "checkbox";
        obox.checked = checked.has(o.name);
        obox.onchange = () => {
          obox.checked ? checked.add(o.name) : checked.delete(o.name);
          renderGroupInfo();
        };
        const member = isMember(o.name);
        const oname = spanEl(o.conflict ? "name conflict" : "name", (member ? "✔ " : "") + o.name);
        if (member) oname.classList.add("member");
        orow.append(obox, oname);
        if (currentSepNode !== null && currentSepName === sec.separator)
          currentSepNode.appendChild(orow);
        else
          frag.appendChild(orow);
      }
    }
    shownOutfits += sec.outfits.length;
  }

  if (display.length === 0) {
    const e = document.createElement("div");
    e.className = "empty";
    e.textContent = S.sections.length === 0
      ? "（尚未扫描到服装——请确认上方选择，或查看日志）"
      : "（没有符合过滤条件的模组）";
    frag.appendChild(e);
  }

  tree.innerHTML = "";
  tree.appendChild(frag);
  $("treeFoot").textContent = `模组 ${display.length} · 服装 ${shownOutfits}`;
  renderGroupInfo();
  renderStatus();
}

function rowDiv(cls) {
  const d = document.createElement("div");
  d.className = cls;
  return d;
}

function spanEl(cls, text) {
  const s = document.createElement("span");
  s.className = cls;
  s.textContent = text;
  return s;
}

function twistBtn(expanded, onclick) {
  const b = document.createElement("button");
  b.className = "twist";
  b.textContent = expanded ? "▾" : "▸";
  b.onclick = onclick;
  return b;
}

// ── 右侧组面板 ──────────────────────────────────────────────────────
function renderGroups() {
  const list = $("groupList");
  list.innerHTML = "";
  for (const g of S.groups) {
    const row = document.createElement("div");
    row.className = "grow" + (g.name === S.currentGroup ? " selected" : "");
    const n = spanEl(null, g.name);
    const c = spanEl("count", String(g.count));
    row.append(n, c);
    row.onclick = () => send({ type: "selectGroup", name: g.name });
    row.ondblclick = openMembers;
    list.appendChild(row);
  }
  if (S.groups.length === 0) {
    const e = document.createElement("div");
    e.className = "empty";
    e.textContent = "（还没有组——点\"新建组\"开始）";
    list.appendChild(e);
  }
}

function renderGroupInfo() {
  const g = currentGroup();
  $("groupInfo").textContent = g ? `当前组：${g.name} · 成员 ${g.count}` : "当前未选中组";
}

// ── 状态栏 / 日志 ───────────────────────────────────────────────────
function renderStatus() {
  $("countsText").textContent =
    `模组 ${S.counts.mods} · 服装 ${S.counts.outfits} · 已分配 ${S.counts.assigned} · 未分配 ${S.counts.unassigned}`;
  const t = $("targetText");
  t.textContent = S.targetDir ? `输出：${S.targetDesc}` : "输出位置未确定";
  t.title = S.targetDir || "";
  if (S.scanning) renderScanBadge();
}

function renderScanBadge() {
  const badge = $("scanBadge");
  badge.hidden = !(S && S.scanning);
  if (S && S.scanning) $("infoLine").textContent += "";
}

function renderMaxGlyph() {
  $("btnMax").textContent = S && S.maximized ? "❐" : "□";
}

function pushLog(line) {
  logLines.push(`[${new Date().toLocaleTimeString("zh-CN", { hour12: false })}] ${line}`);
  if (logLines.length > 500) logLines = logLines.slice(-400);
  const pre = $("logText");
  pre.textContent = logLines.join("\n");
  pre.scrollTop = pre.scrollHeight;
}

function toggleLog() {
  $("logDrawer").hidden = !$("logDrawer").hidden;
}

// ── 加入 / 移出 ─────────────────────────────────────────────────────
function apply(add) {
  const g = currentGroup();
  if (!g) return tip("请先在右侧新建或选中一个组。");
  if (checked.size === 0) return tip("请先在左侧勾选要操作的服装、模组或分隔符。");
  send({ type: "applyToGroup", names: [...checked], add });
  checked.clear();
}

// ── 查看组（模态） ──────────────────────────────────────────────────
function openMembers() {
  const g = currentGroup();
  if (!g) return tip("请先选中一个组。");
  $("membersTitle").textContent = `组「${g.name}」的成员`;
  $("membersFilter").value = "";
  $("membersDialog").showModal();
  renderMembers();
}

function renderMembersIfOpen() {
  if ($("membersDialog").open) renderMembers();
}

function renderMembers() {
  const g = currentGroup();
  const tree = $("membersTree");
  if (!g) return;
  const filter = $("membersFilter").value.trim().toLowerCase();

  const frag = document.createDocumentFragment();
  let shown = 0;
  let curSep = null;
  let sepNode = null;

  for (const sec of S.sections) {
    const memberOutfits = sec.outfits.filter((o) => g.members.includes(o.name));
    if (memberOutfits.length === 0) continue;

    const sepMatch = !filter || (sec.separator || "").toLowerCase().includes(filter);
    const ownerMatch = !filter || sec.owner.toLowerCase().includes(filter);
    const visible = sepMatch || ownerMatch
      ? memberOutfits
      : memberOutfits.filter((o) => o.toLowerCase().includes(filter));
    if (visible.length === 0) continue;

    if (curSep !== sec.separator) {
      curSep = sec.separator;
      sepNode = null;
      if (sec.separator !== null) {
        const r = document.createElement("div");
        r.className = "trow sep";
        r.textContent = sec.separator;
        frag.appendChild(r);
        sepNode = r;
      }
    }

    const mr = document.createElement("div");
    mr.className = "trow mod";
    mr.textContent = `${sec.owner}　(${visible.length})`;
    (sepNode ?? frag).appendChild(mr);

    for (const o of visible) {
      const r = document.createElement("div");
      r.className = "trow outfit";
      const cb = document.createElement("input");
      cb.type = "checkbox";
      cb.dataset.name = o;
      const s = spanEl("name", o);
      s.style.userSelect = "text";
      r.append(cb, s);
      (sepNode ?? frag).appendChild(r);
      shown++;
    }
  }

  tree.innerHTML = "";
  tree.appendChild(frag);
  $("membersCount").textContent = `组内共 ${g.count} 个服装，当前显示 ${shown} 个`;
}

function removeCheckedMembers() {
  const g = currentGroup();
  if (!g) return;
  const names = [...$("membersTree").querySelectorAll("input:checked")]
    .map((cb) => cb.dataset.name)
    .filter(Boolean);
  if (names.length === 0) return tip("请先勾选要移出的服装（可勾选模组或分隔符批量全选）。");
  if (!confirm(`确定把 ${names.length} 个服装移出组「${g.name}」？`)) return;
  send({ type: "applyToGroup", names, add: false });
}

// ── 输入 / 诊断 / 提示 ──────────────────────────────────────────────
function openInput(title, initial, onOk) {
  $("inputTitle").textContent = title;
  $("inputValue").value = initial || "";
  const dialog = $("inputDialog");
  dialog.showModal();

  const submit = () => {
    const v = $("inputValue").value.trim();
    if (dialog.open && v) onOk(v);
  };
  $("inputOk").onclick = () => { submit(); if (dialog.open) dialog.close(); };
  $("inputCancel").onclick = () => dialog.close();
  $("inputForm").onsubmit = (e) => { e.preventDefault(); submit(); dialog.close(); };
  setTimeout(() => { $("inputValue").select(); $("inputValue").focus(); }, 0);
}

function openDiag(text) {
  $("diagText").textContent = text;
  $("diagDialog").showModal();
}

let toastTimer = null;
function tip(message) {
  let el = $("toast");
  if (!el) {
    el = document.createElement("div");
    el.id = "toast";
    document.body.appendChild(el);
  }
  el.textContent = message;
  el.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.remove("show"), 2200);
}
