function main_GWO_NSFD_Tanalysis
    clc; clear;

    % ======== CONFIGURATION ========
    MAXIMIZAR = false; 
    t_end = 200; h = 0.1;
    t = 0:h:t_end; N = length(t);

    % ======== FIXED PARAMETERS ========
    beta = 0.4; omega = 0.1; tau = 0.06;
    alpha = 0.05; gamma = 0.2;
    sigma1 = 0.03; sigma2 = 0.01;
    muS = 0.01; muA = 0.01; muE = 0.01;
    muI = 0.01; muQ = 0.01; muR = 0.01;
    muT = 0.01; phi1 = 0.003;
    eta = 0.02; delta = 0.05;
    B = 10;

    % ======== INITIAL CONDITIONS ========
    S = zeros(1,N); A = S; E = S; I = S;
    Q = S; R = S; Tn = S;
    S(1) = 1000; A(1) = 10; E(1) = 5;
    I(1) = 1; Q(1) = 0; R(1) = 0; Tn(1) = 50;

    % ======== NSFD SIMULATION ========
    for n = 1:N-1
        S(n+1) = (S(n) + h*(B + eta*R(n) + tau*Tn(n))) / (1 + h*(beta*I(n) + alpha + omega + muS));
        A(n+1) = (A(n) + h*alpha*S(n)) / (1 + h*(phi1*I(n) + muA));
        E(n+1) = (E(n) + h*(beta*S(n)*I(n) + phi1*A(n)*I(n))) / (1 + h*(gamma + muE));
        I(n+1) = (I(n) + h*gamma*E(n)) / (1 + h*(sigma1 + sigma2 + muI));
        Q(n+1) = (Q(n) + h*sigma2*I(n)) / (1 + h*(delta + muQ));
        R(n+1) = (R(n) + h*(sigma1*I(n) + delta*Q(n))) / (1 + h*(eta + muR));
        Tn(n+1) = (Tn(n) + h*omega*S(n)) / (1 + h*(tau + muT));
    end

    % ======== TIME OPTIMIZATION WITH GWO ========
    dim = 1;
    SearchAgents_no = 20;
    Max_iter = 50;
    lb = 1; ub = N;

    [bestVal, bestIndex] = GWO(@(x) fitnessTiempoT(x, Tn, MAXIMIZAR), dim, SearchAgents_no, Max_iter, lb, ub);
    bestIndex = round(bestIndex); % convert to integer index
    fprintf('\nOptimal instant of T(t): t = %.1f\n', t(bestIndex));
    fprintf('Value of T(t) = %.2f\n\n', Tn(bestIndex));

    % ======== PLOT ========
    figure;
    plot(t, S, '-b', t, A, '-g', t, E, '-c', t, I, '-r', ...
         t, Q, '-m', t, R, '-k', t, Tn, '-y', 'LineWidth', 2);
    hold on;
    plot(t(bestIndex), Tn(bestIndex), 'kp', 'MarkerSize', 12, 'MarkerFaceColor', 'magenta');
    text(t(bestIndex)+2, Tn(bestIndex), sprintf('T = %.2f', Tn(bestIndex)));
    hold off;
    legend('S (Susceptible)', 'A (Antidote)', 'E (Exposed)', 'I (Infected)', ...
           'Q (Quarantine)', 'R (Recovered)', 'T (Transmission)', 'Peak/Minimum T(t)');
    xlabel('Time');
    ylabel('Number of Devices');
    title('Analysis of T(t) using GWO with fixed parameters');
    grid on;
end

%% === OBJECTIVE FUNCTION FOR GWO ===
function f = fitnessTiempoT(x, Tn, MAXIMIZAR)
    idx = max(1, min(length(Tn), round(x))); % ensure valid range
    f = Tn(idx);
    if ~MAXIMIZAR
        f = -f;
    end
end

%% === GWO ALGORITHM (same as before) ===
function [Alpha_score, Alpha_pos] = GWO(Fobj, dim, SearchAgents_no, Max_iter, lb, ub)
    Alpha_pos = zeros(1, dim);
    Alpha_score = inf;
    Beta_pos = zeros(1, dim);
    Beta_score = inf;
    Delta_pos = zeros(1, dim);
    Delta_score = inf;

    Positions = initialization(SearchAgents_no, dim, ub, lb);

    for l = 1:Max_iter
        for i = 1:size(Positions,1)
            Flag4ub = Positions(i,:) > ub;
            Flag4lb = Positions(i,:) < lb;
            Positions(i,:) = (Positions(i,:).*(~(Flag4ub+Flag4lb))) + ub.*Flag4ub + lb.*Flag4lb;

            fitness = Fobj(Positions(i,:));

            if fitness < Alpha_score
                Alpha_score = fitness;
                Alpha_pos = Positions(i,:);
            elseif fitness < Beta_score
                Beta_score = fitness;
                Beta_pos = Positions(i,:);
            elseif fitness < Delta_score
                Delta_score = fitness;
                Delta_pos = Positions(i,:);
            end
        end

        a = 2 - l * (2/Max_iter);

        for i = 1:size(Positions,1)
            for j = 1:dim
                r1 = rand(); r2 = rand();
                A1 = 2*a*r1 - a;
                C1 = 2*r2;
                D_alpha = abs(C1*Alpha_pos(j) - Positions(i,j));
                X1 = Alpha_pos(j) - A1*D_alpha;

                r1 = rand(); r2 = rand();
                A2 = 2*a*r1 - a;
                C2 = 2*r2;
                D_beta = abs(C2*Beta_pos(j) - Positions(i,j));
                X2 = Beta_pos(j) - A2*D_beta;

                r1 = rand(); r2 = rand();
                A3 = 2*a*r1 - a;
                C3 = 2*r2;
                D_delta = abs(C3*Delta_pos(j) - Positions(i,j));
                X3 = Delta_pos(j) - A3*D_delta;

                Positions(i,j) = (X1 + X2 + X3)/3;
            end
        end
    end
end

function Positions = initialization(SearchAgents_no, dim, ub, lb)
    Positions = zeros(SearchAgents_no, dim);
    for i = 1:dim
        ub_i = ub(i);
        lb_i = lb(i);
        Positions(:,i) = rand(SearchAgents_no,1).*(ub_i - lb_i) + lb_i;
    end
end
