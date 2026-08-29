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
    private const int RandomIndividualCount = 2;
    private const float MaximumCoefficientMagnitude = 5f;

    public static List<AiSaveData> BreedNextGeneration(
        IReadOnlyList<GenerationCandidate> candidates,
        int populationSize,
        float mutationRate,
        float mutationStrength,
        int eliteCount,
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
        int protectedEliteCount = Mathf.Min(
            Mathf.Clamp(eliteCount, 1, 2),
            ranked.Count,
            populationSize);
        for (int index = 0; index < protectedEliteCount; index++)
        {
            nextGeneration.Add(Aidata.CloneData(ranked[index].Genome));
        }

        int randomCount = Mathf.Min(
            RandomIndividualCount,
            populationSize - nextGeneration.Count);
        for (int index = 0; index < randomCount; index++)
        {
            nextGeneration.Add(CreateRandomGenome(ranked[0].Genome, random));
        }

        while (nextGeneration.Count < populationSize)
        {
            GenerationCandidate parentA = SelectByTournament(ranked, random);
            GenerationCandidate parentB = SelectByTournament(ranked, random);
            AiSaveData child = Crossover(parentA.Genome, parentB.Genome, random);
            Mutate(child, mutationRate, mutationStrength, random);
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
        float mutationStrength,
        int eliteCount,
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
            mutationStrength,
            eliteCount,
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
        float mutationStrength,
        System.Random random)
    {
        mutationRate = Mathf.Clamp01(mutationRate);
        mutationStrength = Mathf.Clamp(
            mutationStrength,
            0f,
            populationSetting.MaximumMutationStrength);
        MutateArray(genome.inputToLayer1Weights, mutationRate, mutationStrength, random);
        MutateArray(genome.layer1Biases, mutationRate, mutationStrength, random);
        MutateArray(genome.layer1ToLayer2Weights, mutationRate, mutationStrength, random);
        MutateArray(genome.layer2Biases, mutationRate, mutationStrength, random);
        MutateArray(genome.layer2ToOutputWeights, mutationRate, mutationStrength, random);
        MutateArray(genome.outputBiases, mutationRate, mutationStrength, random);
    }

    private static void MutateArray(
        float[] values,
        float mutationRate,
        float mutationStrength,
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
                values[index] + NextGaussian(random) * mutationStrength,
                -MaximumCoefficientMagnitude,
                MaximumCoefficientMagnitude);
        }
    }

    private static AiSaveData CreateRandomGenome(
        AiSaveData template,
        System.Random random)
    {
        AiSaveData genome = Aidata.CloneData(template);
        RandomizeArray(
            genome.inputToLayer1Weights,
            genome.inputNodeCount,
            genome.layer1NodeCount,
            random);
        Array.Clear(genome.layer1Biases, 0, genome.layer1Biases.Length);
        RandomizeArray(
            genome.layer1ToLayer2Weights,
            genome.layer1NodeCount,
            genome.layer2NodeCount,
            random);
        Array.Clear(genome.layer2Biases, 0, genome.layer2Biases.Length);
        RandomizeArray(
            genome.layer2ToOutputWeights,
            genome.layer2NodeCount,
            genome.outputNodeCount,
            random);
        Array.Clear(genome.outputBiases, 0, genome.outputBiases.Length);
        return genome;
    }

    private static void RandomizeArray(
        float[] values,
        int inputCount,
        int outputCount,
        System.Random random)
    {
        if (values == null)
        {
            return;
        }

        float limit = Mathf.Sqrt(
            6f / (Mathf.Max(1, inputCount) + Mathf.Max(1, outputCount)));
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = ((float)random.NextDouble() * 2f - 1f) * limit;
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
