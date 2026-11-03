namespace ExecutionFlow.Examples.Hosting
{
    /// <summary>A minimal page that shows the plan and the latest runs, refreshing every 2 seconds.</summary>
    internal static class Page
    {
        public const string Html = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>ExecutionFlow.Hosting example</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 2rem; }
    table { border-collapse: collapse; margin-bottom: 2rem; }
    th, td { border: 1px solid #ccc; padding: .3rem .6rem; text-align: left; }
    .Failed { color: #b00; } .Succeeded { color: #070; }
  </style>
</head>
<body>
  <h1>ExecutionFlow.Hosting</h1>
  <button id="run-once">Run every handler once</button>
  <pre id="run-once-result"></pre>
  <h2>Plan</h2>
  <table id="plan"><thead><tr><th>Handler</th><th>Enabled</th><th>Mode</th><th>After</th><th>Min interval</th></tr></thead><tbody></tbody></table>
  <h2>Runs</h2>
  <table id="runs"><thead><tr><th>Handler</th><th>Started</th><th>Ended</th><th>Status</th></tr></thead><tbody></tbody></table>
  <script>
    const cell = v => `<td>${v ?? ''}</td>`;
    async function refresh() {
      const plan = await (await fetch('/api/plan')).json();
      document.querySelector('#plan tbody').innerHTML = plan.map(s =>
        `<tr data-handler="${s.handler}">${cell(s.handler)}${cell(s.enabled)}${cell(s.mode)}${cell(s.prerequisites.join(', '))}${cell(s.minInterval)}</tr>`).join('');
      const runs = await (await fetch('/api/runs?take=50')).json();
      document.querySelector('#runs tbody').innerHTML = runs.map(r =>
        `<tr data-handler="${r.handler}" class="${r.status.split(':')[0]}">${cell(r.handler)}${cell(r.startedAt)}${cell(r.endedAt)}${cell(r.status)}</tr>`).join('');
    }
    document.getElementById('run-once').onclick = async () => {
      const result = await (await fetch('/api/run-once', { method: 'POST' })).json();
      document.getElementById('run-once-result').textContent = JSON.stringify(result, null, 2);
    };
    refresh();
    setInterval(refresh, 2000);
  </script>
</body>
</html>
""";
    }
}
