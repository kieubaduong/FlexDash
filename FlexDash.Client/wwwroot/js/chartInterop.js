window.FlexDash = (function () {
    const charts = {};

    // Grafana-style dark theme defaults
    const gridColor = '#2a2d35';
    const tickColor = '#5a6069';
    const lineColor = '#3274d9';
    const lineFill = 'rgba(50, 116, 217, 0.15)';
    const gaugeTrack = '#2a2d35';

    function createLineChart(canvasId, labels, data) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (charts[canvasId]) {
            charts[canvasId].destroy();
        }

        charts[canvasId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Value',
                    data: data,
                    borderColor: lineColor,
                    backgroundColor: lineFill,
                    borderWidth: 2,
                    pointRadius: 0,
                    pointHoverRadius: 3,
                    tension: 0.3,
                    fill: true
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                scales: {
                    x: {
                        display: true,
                        grid: { color: gridColor },
                        ticks: { color: tickColor, font: { size: 10 } }
                    },
                    y: {
                        display: true,
                        beginAtZero: true,
                        grid: { color: gridColor },
                        ticks: { color: tickColor, font: { size: 10 } }
                    }
                },
                plugins: { legend: { display: false } }
            }
        });
    }

    function updateLineChart(canvasId, labels, data) {
        const chart = charts[canvasId];
        if (!chart) return;

        chart.data.labels = labels;
        chart.data.datasets[0].data = data;
        chart.update('none');
    }

    function createGaugeChart(canvasId, value, min, max) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (charts[canvasId]) {
            charts[canvasId].destroy();
        }

        const pct = Math.min(Math.max((value - min) / (max - min), 0), 1);
        const remaining = 1 - pct;

        charts[canvasId] = new Chart(ctx, {
            type: 'doughnut',
            data: {
                datasets: [{
                    data: [pct, remaining],
                    backgroundColor: [getGaugeColor(pct), gaugeTrack],
                    borderWidth: 0,
                    circumference: 180,
                    rotation: 270
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                cutout: '70%',
                plugins: { legend: { display: false }, tooltip: { enabled: false } }
            }
        });
    }

    function updateGauge(canvasId, value, min, max) {
        const chart = charts[canvasId];
        if (!chart) return;

        const pct = Math.min(Math.max((value - min) / (max - min), 0), 1);
        const remaining = 1 - pct;

        chart.data.datasets[0].data = [pct, remaining];
        chart.data.datasets[0].backgroundColor = [getGaugeColor(pct), gaugeTrack];
        chart.update('none');
    }

    function getGaugeColor(pct) {
        if (pct < 0.6) return '#73bf69';
        if (pct < 0.8) return '#ff9830';
        return '#e02f44';
    }

    return { createLineChart, updateLineChart, createGaugeChart, updateGauge };
})();
