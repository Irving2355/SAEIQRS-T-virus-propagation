%case_1
% beta = 0.5; tau = 0.05; sigma1 = 0.01; sigma2 = 0.05;
%case_2
% beta = 0.2; tau = 0.08; sigma1 = 0.01; sigma2 = 0.05;
%case_3
% beta = 0.4; alpha = 0.05; tau = 0.06; sigma2 = 0.01; sigma1 = 0.03;

function virus_propagation_simulation
    % Define model parameters
    beta = 0.4; alpha = 0.05; gamma = 0.2;
    sigma1 = 0.03; sigma2 = 0.01;
    omega = 0.1; tau = 0.06; muS = 0.01;
    muA = 0.01; muE = 0.01; muI = 0.01;
    muQ = 0.01; muR = 0.01; muT = 0.01;
    phi1 = 0.003; eta = 0.02; delta = 0.05; B = 0;

    % Initial conditions
    S0 = 1000; A0 = 10; E0 = 5; I0 = 1;
    Q0 = 0; R0 = 0; T0 = 50;
    y0 = [S0 A0 E0 I0 Q0 R0 T0];
    tspan = [0 200];

    % Solve the ODE system
    [t, y] = ode45(@(t,y) saeiqrst_model(t, y, beta, alpha, gamma, sigma1, ...
        sigma2, omega, tau, muS, muA, muE, muI, muQ, muR, muT, phi1, eta, delta, B), ...
        tspan, y0);

    % Plot the results
    figure; hold on;
    plot(t, y(:,1), 'b', 'LineWidth', 2); % S
    plot(t, y(:,2), 'g', 'LineWidth', 2); % A
    plot(t, y(:,3), 'c', 'LineWidth', 2); % E
    plot(t, y(:,4), 'r', 'LineWidth', 2); % I
    plot(t, y(:,5), 'm', 'LineWidth', 2); % Q
    plot(t, y(:,6), 'k', 'LineWidth', 2); % R
    plot(t, y(:,7), 'y', 'LineWidth', 2); % T
    legend('S','A','E','I','Q','R','T');
    xlabel('Time'); ylabel('Number of computers');
    title('Virus propagation simulation');
    grid on; hold off;
end

function dydt = saeiqrst_model(~, y, beta, alpha, gamma, sigma1, sigma2, omega, ...
    tau, muS, muA, muE, muI, muQ, muR, muT, phi1, eta, delta, B)

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