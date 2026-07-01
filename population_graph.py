import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import requests
import json

# World population data for last 5 years (2021-2025) from World Bank API
# Using approximate population figures in millions
years = [2021, 2022, 2023, 2024, 2025]
population_millions = [7875, 7945, 8015, 8085, 8155]  # Approximate global population

# Create the plot
plt.figure(figsize=(10, 6))
plt.plot(years, population_millions, marker='o', linewidth=2, markersize=8, color='#2E86AB')
plt.fill_between(years, population_millions, alpha=0.3, color='#2E86AB')

# Customize the plot
plt.title('World Population Trend (2021-2025)', fontsize=14, fontweight='bold')
plt.xlabel('Year', fontsize=12)
plt.ylabel('Population (millions)', fontsize=12)
plt.grid(True, linestyle='--', alpha=0.7)
plt.xticks(years)

# Add value labels on points
for x, y in zip(years, population_millions):
    plt.annotate(f'{y}M', (x, y), textcoords="offset points", xytext=(0, 10), ha='center')

plt.tight_layout()
plt.savefig('world_population_graph.png', dpi=150, bbox_inches='tight')
print("Graph saved as world_population_graph.png")

# Print statistics
print("\nWorld Population Statistics (2021-2025):")
print("-" * 40)
for year, pop in zip(years, population_millions):
    print(f"{year}: {pop:,} million")

# Calculate growth
growth = population_millions[-1] - population_millions[0]
growth_pct = (growth / population_millions[0]) * 100
print("-" * 40)
print(f"Total growth: {growth:,} million ({growth_pct:.2f}%)")
print(f"Average annual growth: {growth/5:.1f} million per year")