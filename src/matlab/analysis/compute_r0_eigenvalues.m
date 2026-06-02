clc; clear; close all;

% Define symbolic variables
syms I S beta gamma phi_1 A mu_A mu_E mu_I sigma_1 sigma_2

% Define the denominator D
D = A * I * gamma * phi_1^2 - A * mu_A * gamma * phi_1 + ...
    mu_A * mu_I * gamma + mu_A * sigma_1 * gamma + mu_A * sigma_2 * gamma + ...
    mu_A * mu_E * mu_I + mu_A * mu_E * sigma_1 + mu_A * mu_E * sigma_2;

% Define the simplified matrix $FV^{-1}$
FV_inv = [  
0,      0,      0,      0;
(I * S * beta * gamma * phi_1)/D,(S * beta *gamma * mu_A)/D,
(S * beta * mu_A * (gamma + mu_E)) / D,         0;
0,      0,      0,      0;
0,      0,      0,      0];

% Calculate eigenvalues
eigenvalues = eig(FV_inv);

% Show results
disp('Eigenvalues of FV^-1:')
disp(simplify(eigenvalues))