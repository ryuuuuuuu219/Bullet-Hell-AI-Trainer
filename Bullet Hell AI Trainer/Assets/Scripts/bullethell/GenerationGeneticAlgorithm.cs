using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GenerationCandidate
{
    public int LogicalLayer { get; set; }
    public AiSaveData Genome { get; set; }
    public float Damage { get; set; }
    public float SurvivalTime { get; set; }
    public float EdgeCollisionCumulativeTime { get; set; }
    public float CenterDistanceSampledSum { get; set; }
    public float Score { get; set; }

    public float GetAxisQuality(GeneticSaveEvaluationAxis axis)
    {
        switch (axis)
        {
            case GeneticSaveEvaluationAxis.Damage:
                return Damage;
            case GeneticSaveEvaluationAxis.SurvivalTime:
                return SurvivalTime;
            case GeneticSaveEvaluationAxis.EdgeCollisionCumulativeTime:
                return -EdgeCollisionCumulativeTime;
            case GeneticSaveEvaluationAxis.CenterDistanceSampledSum:
                return -CenterDistanceSampledSum;
            default:
                return Score;
        }
    }
}

public static class GenerationGeneticAlgorithm
{
    private const float MutationStrength = 0.1f;
    private const float MaximumCoefficientMagnitude = 5f;

    public static List<AiSaveData> BreedNextGeneration(
        IReadOnlyList<GenerationCandidate> candidates,
        int populationSize,
        float mutationRate,
        int seed)
    {
        List<AiSaveData> nextGeneration = new List<AiSaveData>();
        if (candidates == null || candidates.Count == 0 || populationSize <= 0)
        {
            return nextGeneration;
        }

        List<GenerationCandidate> ranked = new List<GenerationCandidate>(candidates);
        ranked.Sort((left, right) => right.Score.CompareTo(left.Score));

        System.Random random = new System.Random(seed);
        nextGeneration.Add(Aidata.CloneData(ranked[0].Genome));

        while (nextGeneration.Count < populationSize)
        {
            GenerationCandidate parentA = SelectByTournament(ranked, random);
            GenerationCandidate parentB = SelectByTournament(ranked, random);
            AiSaveData child = Crossover(parentA.Genome, parentB.Genome, random);
            Mutate(child, mutationRate, random);
            nextGeneration.Add(child);
        }

        return nextGeneration;
    }

    public static GenerationCandidate SelectGenomeToSave(
        IReadOnlyList<GenerationCandidate> candidates,
        GeneticSaveEvaluationAxis axis)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        GenerationCandidate best = candidates[0];
        float bestQuality = best.GetAxisQuality(axis);
        for (int index = 1; index < candidates.Count; index++)
        {
            GenerationCandidate candidate = candidates[index];
            float quality = candidate.GetAxisQuality(axis);
            if (quality > bestQuality ||
                (Mathf.Approximately(quality, bestQuality) &&
                 candidate.Score > best.Score))
            {
                best = candidate;
                bestQuality = quality;
            }
        }

        return best;
    }

    public static List<AiSaveData> CreatePopulationFromSavedGenome(
        AiSaveData savedGenome,
        int populationSize,
        float mutationRate,
        int seed)
    {
        GenerationCandidate source = new GenerationCandidate
        {
            Genome = Aidata.CloneData(savedGenome),
            Score = 0f,
        };
        return BreedNextGeneration(
            new[] { source },
            populationSize,
            mutationRate,
            seed);
    }

    private static GenerationCandidate SelectByTournament(
        IReadOnlyList<GenerationCandidate> candidates,
        System.Random random)
    {
        GenerationCandidate first = candidates[random.Next(candidates.Count)];
        GenerationCandidate second = candidates[random.Next(candidates.Count)];
        return first.Score >= second.Score ? first : second;
    }

    private static AiSaveData Crossover(
        AiSaveData parentA,
        AiSaveData parentB,
        System.Random random)
    {
        AiSaveData child = Aidata.CloneData(parentA);
        child.inputToLayer1Weights = CrossoverArray(
            parentA.inputToLayer1Weights,
            parentB.inputToLayer1Weights,
            random);
        child.layer1Biases = CrossoverArray(
            parentA.layer1Biases,
            parentB.layer1Biases,
            random);
        child.layer1ToLayer2Weights = CrossoverArray(
            parentA.layer1ToLayer2Weights,
            parentB.layer1ToLayer2Weights,
            random);
        child.layer2Biases = CrossoverArray(
            parentA.layer2Biases,
            parentB.layer2Biases,
            random);
        child.layer2ToOutputWeights = CrossoverArray(
            parentA.layer2ToOutputWeights,
            parentB.layer2ToOutputWeights,
            random);
        child.outputBiases = CrossoverArray(
            parentA.outputBiases,
            parentB.outputBiases,
            random);
        return child;
    }

    private static float[] CrossoverArray(
        float[] parentA,
        float[] parentB,
        System.Random random)
    {
        parentA ??= Array.Empty<float>();
        parentB ??= Array.Empty<float>();
        int length = Mathf.Min(parentA.Length, parentB.Length);
        float[] child = new float[parentA.Length];

        for (int index = 0; index < length; index++)
        {
            child[index] = random.NextDouble() < 0.5
                ? parentA[index]
                : parentB[index];
        }

        if (length < parentA.Length)
        {
            Array.Copy(parentA, length, child, length, parentA.Length - length);
        }

        return child;
    }

    private static void Mutate(
        AiSaveData genome,
        float mutationRate,
        System.Random random)
    {
        mutationRate = Mathf.Clamp01(mutationRate);
        MutateArray(genome.inputToLayer1Weights, mutationRate, random);
        MutateArray(genome.layer1Biases, mutationRate, random);
        MutateArray(genome.layer1ToLayer2Weights, mutationRate, random);
        MutateArray(genome.layer2Biases, mutationRate, random);
        MutateArray(genome.layer2ToOutputWeights, mutationRate, random);
        MutateArray(genome.outputBiases, mutationRate, random);
    }

    private static void MutateArray(
        float[] values,
        float mutationRate,
        System.Random random)
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (random.NextDouble() >= mutationRate)
            {
                continue;
            }

            values[index] = Mathf.Clamp(
                values[index] + NextGaussian(random) * MutationStrength,
                -MaximumCoefficientMagnitude,
                MaximumCoefficientMagnitude);
        }
    }

    private static float NextGaussian(System.Random random)
    {
        double first = Math.Max(double.Epsilon, random.NextDouble());
        double second = random.NextDouble();
        return (float)(Math.Sqrt(-2d * Math.Log(first)) *
                       Math.Cos(2d * Math.PI * second));
    }
}
