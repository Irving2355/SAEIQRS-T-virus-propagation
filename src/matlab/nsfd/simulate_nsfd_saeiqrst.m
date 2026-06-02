%case_1
% beta = 0.5; tau = 0.05; sigma1 = 0.01; sigma2 = 0.05;
%case_2
% beta = 0.2; tau = 0.08; sigma1 = 0.01; sigma2 = 0.05;
%case_3
% beta = 0.4; alpha = 0.05; tau = 0.06; sigma2 = 0.01; sigma1 = 0.03;

% SAEIQRS-T model using NSFD (to compare with ode45)

% Time settings
h = 0.1; t_end = 200;
t = 0:h:t_end;
N = length(t);

% Model parameters
beta = 0.4; alpha = 0.05; gamma = 0.2;
sigma1 = 0.03; sigma2 = 0.01;
omega = 0.1; tau = 0.06; muS = 0.01;
muA = 0.01; muE = 0.01; muI = 0.01;
muQ = 0.01; muR = 0.01; muT = 0.01;
phi1 = 0.003; eta = 0.02; delta = 0.05;
B = 10;

% Initial conditions
S0 = 1000; A0 = 10; E0 = 5;
I0 = 1; Q0 = 0; R0 = 0; T0 = 50;

% Initialize vectors
S = zeros(1, N); A = zeros(1, N); E = zeros(1, N);
I = zeros(1, N); Q = zeros(1, N); R = zeros(1, N); T = zeros(1, N);

S(1) = S0; A(1) = A0; E(1) = E0;
I(1) = I0; Q(1) = Q0; R(1) = R0; T(1) = T0;

% NSFD iteration
for n = 1:N-1
    denom_S = 1 + h * (beta*I(n) + alpha + omega + muS);
    S(n+1) = (S(n) + h * (B + eta * R(n) + tau * T(n))) / denom_S;

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
    T(n+1) = (T(n) + h * omega * S(n)) / denom_T;
end

% Plotting
figure;
plot(t, S, '-b', t, A, '-g', t, E, '-c', t, I, '-r', ...
     t, Q, '-m', t, R, '-k', t, T, '-y', 'LineWidth', 2);
legend('S (Susceptible)', 'A (Antidote)', 'E (Exposed)', 'I (Infected)', ...
       'Q (Quarantine)', 'R (Recovered)', 'T (Transmission)');
xlabel('Time'); ylabel('Number of Devices');
title('NSFD simulation');
grid on;