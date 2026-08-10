const snippets = [
  { id: 1, title: "Resposta de acompanhamento", trigger: "/followup", category: "E-mails", uses: 128, body: "Olá {{nome}},\n\nConforme conversamos, seguem os próximos passos para acompanhamento:\n\n1. Revisar o material até {{data}}\n2. Confirmar os responsáveis\n3. Retornar com eventuais dúvidas\n\nObrigado!" },
  { id: 2, title: "Solicitar evidências", trigger: "/evidencias", category: "Trabalho", uses: 91, body: "Olá {{nome}}, poderia encaminhar as evidências referentes ao período {{mes}}?" },
  { id: 3, title: "Encerramento de chamado", trigger: "/encerrar", category: "Suporte", uses: 76, body: "O chamado foi concluído. Caso o problema volte a ocorrer, responda esta mensagem." },
  { id: 4, title: "Agendar reunião", trigger: "/reuniao", category: "Trabalho", uses: 63, body: "Olá {{nome}}, podemos agendar uma conversa em {{data}} às {{hora}}?" },
  { id: 5, title: "Ausência temporária", trigger: "/ausente", category: "Pessoal", uses: 34, body: "Estarei ausente até {{data}}. Em caso de urgência, procure {{responsavel}}." },
  { id: 6, title: "Confirmar recebimento", trigger: "/recebido", category: "E-mails", uses: 22, body: "Olá {{nome}}, confirmo o recebimento. Retorno assim que concluir a análise." }
];

const categories = [{ name: "Todos", count: 24 }, { name: "Trabalho", count: 9 }, { name: "E-mails", count: 6 }, { name: "Suporte", count: 5 }, { name: "Pessoal", count: 4 }];
const variables = [["{{nome}}", "Nome informado ao expandir"], ["{{data}}", "Data atual ou escolhida"], ["{{hora}}", "Horário atual"], ["{{mes}}", "Mês por extenso"], ["{{usuario}}", "Usuário do Windows"], ["{{responsavel}}", "Campo preenchível"]];
const tabs = [["⌘", "Atalhos"], ["Á", "Acento Rápido"], ["▣", "Captura"], ["↗", "Estatísticas"], ["⚙", "Configurações"], ["ⓘ", "Sobre"]];

let selectedCategory = "Todos";
let selectedFilter = "Todos";
let selectedId = 1;

const pageParams = new URLSearchParams(window.location.search);
if (pageParams.get("capture") === "1") document.querySelector(".review-stage").classList.add("capture-mode");
if (pageParams.get("theme") === "dark") {
  document.querySelector(".review-stage").dataset.theme = "dark";
  document.getElementById("themeToggle").textContent = "☀";
}

document.getElementById("topnav").innerHTML = tabs.map(([icon, label], index) => `<button class="${index === 0 ? "active" : ""}"><span class="nav-icon">${icon}</span>${label}</button>`).join("");
document.getElementById("categoryGrid").innerHTML = categories.map((item) => `<button class="category-chip ${item.name === "Todos" ? "selected" : ""}" data-category="${item.name}"><span>${item.name}</span><b>${item.count}</b></button>`).join("");
document.getElementById("variableList").innerHTML = variables.map(([token, description]) => `<button><code>${token.replaceAll("<", "&lt;")}</code><span>${description}</span><b>＋</b></button>`).join("");

function renderList() {
  const query = document.getElementById("searchInput").value.trim().toLowerCase();
  const visible = snippets
    .filter((item) => selectedCategory === "Todos" || item.category === selectedCategory)
    .filter((item) => !query || `${item.title} ${item.trigger} ${item.category} ${item.body}`.toLowerCase().includes(query))
    .filter((item) => selectedFilter === "Todos" || item.uses >= 60)
    .sort((a, b) => selectedFilter === "Mais utilizados" ? b.uses - a.uses : a.id - b.id);

  document.getElementById("snippetCount").textContent = `${visible.length} de 24`;
  document.getElementById("snippetList").innerHTML = visible.length ? visible.map((item) => `<button class="snippet-card ${selectedId === item.id ? "selected" : ""}" data-id="${item.id}"><span class="snippet-title">${item.title}</span><span class="snippet-meta"><code>${item.trigger}</code><em>${item.category}</em></span></button>`).join("") : `<div class="empty-state"><span>⌕</span><strong>Nenhum atalho encontrado</strong><small>Ajuste a busca ou os filtros.</small></div>`;
  document.querySelectorAll(".snippet-card").forEach((button) => button.addEventListener("click", () => { selectedId = Number(button.dataset.id); renderList(); renderEditor(); }));
}

function renderEditor() {
  const item = snippets.find((snippet) => snippet.id === selectedId) || snippets[0];
  document.getElementById("editorTitle").textContent = item.title;
  document.getElementById("nameInput").value = item.title;
  document.getElementById("triggerInput").value = item.trigger.slice(1);
  document.getElementById("categorySelect").innerHTML = `<option>${item.category}</option>`;
  document.getElementById("bodyInput").value = item.body;
  document.getElementById("previewText").textContent = item.body.replace("{{nome}}", "Marina").replace("{{data}}", "12 de agosto de 2026").replace("{{hora}}", "14:30").replace("{{mes}}", "agosto").replace("{{responsavel}}", "a equipe responsável");
}

document.querySelectorAll(".category-chip").forEach((button) => button.addEventListener("click", () => {
  selectedCategory = button.dataset.category;
  document.querySelectorAll(".category-chip").forEach((item) => item.classList.toggle("selected", item === button));
  renderList();
}));
document.querySelectorAll("#displayFilter button").forEach((button) => button.addEventListener("click", () => {
  selectedFilter = button.dataset.filter;
  document.querySelectorAll("#displayFilter button").forEach((item) => item.classList.toggle("selected", item === button));
  renderList();
}));
document.getElementById("searchInput").addEventListener("input", renderList);
document.addEventListener("keydown", (event) => { if (event.ctrlKey && event.key.toLowerCase() === "k") { event.preventDefault(); document.getElementById("searchInput").focus(); } });
document.getElementById("themeToggle").addEventListener("click", () => {
  const stage = document.querySelector(".review-stage");
  const dark = stage.dataset.theme === "dark";
  stage.dataset.theme = dark ? "light" : "dark";
  document.getElementById("themeToggle").textContent = dark ? "☾" : "☀";
});

renderList();
renderEditor();
