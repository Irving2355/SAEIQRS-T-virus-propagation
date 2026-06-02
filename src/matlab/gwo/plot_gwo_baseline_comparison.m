clear; clc; close all;

% ===== CSV PATHS =====
base_file = 'results/agent_based/resultado_agentes.csv';
gwo_file  = 'results/agent_based/resultado_agentes_gwo.csv';

% ===== READ TABLES =====
Tb = readtable(base_file);
Tg = readtable(gwo_file);

% ===== DETECT TIME-STEP COLUMN =====
namesB = Tb.Properties.VariableNames;
namesG = Tg.Properties.VariableNames;

if any(strcmpi(namesB,'step'))
    t_b = Tb.step;
elseif any(strcmpi(namesB,'Paso'))
    t_b = Tb.Paso;
else
    error('No time column found in baseline.');
end

if any(strcmpi(namesG,'step'))
    t_g = Tg.step;
elseif any(strcmpi(namesG,'Paso'))
    t_g = Tg.Paso;
else
    error('No time column found in GWO.');
end

% ===== INFECTED CURVES =====
I_b = Tb.I;
I_g = Tg.I;

% ===== MATCH LENGTH =====
n = min([length(t_b), length(t_g), length(I_b), length(I_g)]);
t_b = t_b(1:n); I_b = I_b(1:n);
t_g = t_g(1:n); I_g = I_g(1:n);

refine = 50;

% ===== DENSIFIED TIME =====
t_smooth = linspace(1, n, n * refine);

% ===== SMOOTH SPLINE-TYPE INTERPOLATION =====
I_b_s = interp1(t_b, I_b, t_smooth, 'spline');
I_g_s = interp1(t_g, I_g, t_smooth, 'spline');

% ===== SMOOTH COMPARATIVE PLOT =====
figure; hold on; grid on;

plot(t_smooth, I_b_s, 'r', 'LineWidth', 2.0);
plot(t_smooth, I_g_s, 'b', 'LineWidth', 2.0);

xlabel('Time step');
ylabel('Infected I(t)');
title('Baseline ABM vs ABM + GWO');
legend('Baseline', 'With GWO', 'Location', 'northeast');

% ===== PEAKS AND REDUCTION (UNSMOOTHED: REAL DATA) =====
peak_base = max(I_b);
peak_gwo  = max(I_g);
reduction = 100 * (peak_base - peak_gwo) / peak_base;

fprintf('Peak I baseline : %g\n', peak_base);
fprintf('Peak I with GWO : %g\n', peak_gwo);
fprintf('Reduction       : %.2f %%\n', reduction);

