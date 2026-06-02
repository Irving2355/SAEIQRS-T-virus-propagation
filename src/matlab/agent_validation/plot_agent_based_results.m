archivo = 'results/agent_based/resultado_agentes.csv';

if exist('readtable','file')
    % Preferred: respects headers and types
    T = readtable(archivo,'Delimiter',',');
    % Ensures that variables exist with these names:
    t = T.step;
    S = T.S;
    A = T.A;
    E = T.E;
    I = T.I;
    Q = T.Q;
    R = T.R;
    Tn = T.T;  
elseif exist('csvread','file')
    % Fallback for old versions: skip 1 header row
    M = csvread(archivo, 1, 0);
    t  = M(:,1);
    S  = M(:,2);
    A  = M(:,3);
    E  = M(:,4);
    I  = M(:,5);
    Q  = M(:,6);
    R  = M(:,7);
    Tn = M(:,8);
else
    error('Your MATLAB version does not have readtable or csvread. Try dlmread or update MATLAB.');
end

% ===== PLOT =====
figure; hold on; grid on;
plot(t, S, 'b', 'LineWidth', 2);
plot(t, A, 'g', 'LineWidth', 2);
plot(t, E, 'c', 'LineWidth', 2);
plot(t, I, 'r', 'LineWidth', 2);
plot(t, Q, 'm', 'LineWidth', 2);
plot(t, R, 'k', 'LineWidth', 2);
plot(t, Tn, 'y', 'LineWidth', 2);
legend('S','A','E','I','Q','R','T', 'Location','best');
xlabel('Time (Step)');
ylabel('Agents');
title('Agent-based simulation comparison');
