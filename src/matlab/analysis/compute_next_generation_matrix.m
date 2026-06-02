clc; clear; close all;

% Define symbolic variables
syms mu_A phi_1 A I gamma mu_E sigma_1 sigma_2 mu_I delta beta S

% Define the original matrix V (which is already multiplied by -1)
V = [-mu_A, 0, -phi_1*A, 0;
      phi_1*I, -gamma - mu_E, phi_1*A, 0;
      0, gamma, -sigma_1 - sigma_2 - mu_I, 0;
      0, 0, sigma_1, delta];

% Define the matrix F
F = [0, 0, 0, 0;
     0, 0, beta*S, 0;
     0, 0, 0, 0;
     0, 0, 0, 0];

% Calculate the inverse of V
V_inv = inv(-V); % Since V is originally multiplied by -1, -V is inverted

% Calculate the next-generation matrix $FV^{-1}$
NextGenMatrix = F * V_inv;

% Show result
disp('Matrix FV^-1:')
disp(simplify(NextGenMatrix))