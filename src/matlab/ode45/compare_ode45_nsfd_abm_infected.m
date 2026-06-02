% Comparison of I(t): ODE45 vs NSFD vs ABM (CSV) -- SAEIQRS-T
% Using your original parameters (Case 1) but plotting I(t)
% normalised by its own maximum so the three curves are comparable.


clear; clc;

%% ========= 1) COMMON PARAMETERS ================
beta   = 0.4;   % Infection rate
alpha  = 0.05;  % Immunization rate
gamma  = 0.2;   % Transition from Exposed to Infected
sigma1 = 0.03;  % Recovery rate
sigma2 = 0.01;  % Quarantine rate
omega  = 0.1;   % Conversion to transmission nodes
tau    = 0.06;  % Exit from transmission nodes

muS = 0.01;   % Mortality rate
muA = 0.01; muE = 0.01; muI = 0.01;
muQ = 0.01; muR = 0.01; muT = 0.01;

phi1 = 0.003;
eta  = 0.02;
delta = 0.05;
B     = 10;

% Initial conditions
S0 = 9100;
A0 = 100;
E0 = 250;
I0 = 350;
Q0 = 150;
R0 = 50;
T0 = 333;

%% ========= 2) ODE45 ==========
y0    = [S0 A0 E0 I0 Q0 R0 T0];
tspan = 0:1:1500;

[t_ode, y_ode] = ode45(@(t,y) saeiqrst_model(t, y, beta, alpha, gamma, ...
    sigma1, sigma2, omega, tau, muS, muA, muE, muI, ...
    muQ, muR, muT, phi1, eta, delta, B), tspan, y0);

I_ode = y_ode(:,4);

%% ========= 3) NSFD ==========
h     = 1.0;
t_end = 1500;
t_ns  = 0:h:t_end;
N     = length(t_ns);

S = zeros(1, N); A = zeros(1, N); E = zeros(1, N);
I = zeros(1, N); Q = zeros(1, N); R = zeros(1, N); Tn = zeros(1, N);

S(1) = S0; A(1) = A0; E(1) = E0;
I(1) = I0; Q(1) = Q0; R(1) = R0; Tn(1) = T0;

for n = 1:N-1
    denom_S = 1 + h * (beta*I(n) + alpha + omega + muS);
    S(n+1) = (S(n) + h * (B + eta * R(n) + tau * Tn(n))) / denom_S;

    denom_A = 1 + h * (phi1 * I(n) + muA);
    A(n+1) = (A(n) + h * alpha * S(n)) / denom_A;

    denom_E = 1 + h * (gamma + muE);
    E(n+1) = (E(n) + h * (beta * S(n) * I(n) + phi1 * A(n) * I(n))) / denom_E;

    denom_I = 1 + h * (sigma1 + sigma2 + muI);
    I(n+1) = (I(n) + h * gamma * E(n)) / denom_I;

    denom_Q = 1 + h * (delta + muQ);
    Q(n+1) = (Q(n) + h * sigma2 * I(n)) / denom_Q;

    denom_R = 1 + h * (eta + muR);
    R(n+1) = (R(n) + h * (sigma1 * I(n) + delta * Q(n))) / denom_R;

    denom_T = 1 + h * (tau + muT);
    Tn(n+1) = (Tn(n) + h * omega * S(n)) / denom_T;
end

I_nsfd = I(:);   % columna

%% ========= 4) ABM (CSV) ==========
csv_file = 'results/agent_based/resultado_agentes.csv';

if exist('readtable','file')
    TAB   = readtable(csv_file,'Delimiter',',');
    t_csv = TAB.step;
    I_abm = TAB.I;
else
    M     = csvread(csv_file, 1, 0);
    t_csv = M(:,1);
    I_abm = M(:,5);
end

%% ========= 5) Normalise all I(t) to compare shapes ==========
I_ode_n   = I_ode   / max(I_ode);
I_nsfd_n  = I_nsfd  / max(I_nsfd);
I_abm_n   = I_abm   / max(I_abm);

% (opcional) ver picos reales para comentarlo en el texto
fprintf('Max I:  ODE45 = %.2f,  NSFD = %.2f,  ABM = %.2f\n', ...
        max(I_ode), max(I_nsfd), max(I_abm));

%% ========= 6) Plot comparison ==========
figure; hold on; grid on;

plot(t_csv, I_abm_n, 'r',  'LineWidth', 2.0);
plot(t_ns,  I_nsfd_n,'b--','LineWidth', 2.0);
plot(t_ode, I_ode_n, 'k',  'LineWidth', 2.0);

xlabel('Time (t)','FontSize',12);
ylabel('Normalised infected I(t)','FontSize',12);
title('Normalised comparison: ABM vs ODE45 vs NSFD','FontSize',14);
legend('ABM (agents)','NSFD','ODE45','Location','northeast');

% saveas(gcf,'pictures/abm_vs_ode_nsfd_case1_normalised.jpg');

%% ========= Local ODE model ==========
function dydt = saeiqrst_model(~, y, beta, alpha, gamma, sigma1, sigma2, ...
    omega, tau, muS, muA, muE, muI, muQ, muR, muT, phi1, eta, delta, B)

    S = y(1); A = y(2); E = y(3); I = y(4);
    Q = y(5); R = y(6); T = y(7);

    dS = B - beta*S*I - alpha*S - omega*S + eta*R + tau*T - muS*S;
    dA = alpha*S - phi1*A*I - muA*A;
    dE = beta*S*I + phi1*A*I - gamma*E - muE*E;
    dI = gamma*E - (sigma1 + sigma2)*I - muI*I;
    dQ = sigma2*I - delta*Q - muQ*Q;
    dR = sigma1*I + delta*Q - eta*R - muR*R;
    dT = omega*S - tau*T - muT*T;

    dydt = [dS; dA; dE; dI; dQ; dR; dT];
end