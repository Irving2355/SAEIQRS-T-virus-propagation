clear; clc; close all;

conv = readtable('../gwo_convergence.csv');

iter = conv.Iter;
fitness = conv.BestFitness;

figure; hold on; grid on;
plot(iter, fitness, 'b-', 'LineWidth', 2);

xlabel('Iteration');
ylabel('Best Fitness (Peak I)');
title('GWO Convergence — Star Topology');

saveas(gcf, 'pictures/gwo_convergence_star.jpg');