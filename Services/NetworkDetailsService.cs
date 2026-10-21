using System;
using System.Collections.Generic;
using System.Linq;
using SemanticExploverApp.Models;

namespace SemanticExploverApp.Services;

public sealed class NetworkDetailsService { // сведения о выбранном узле
    // собираем описание, связи и источники узла
    public NodeDetails GetDetails(SemanticNetwork network, SemanticNode? selectedNode) {
        ArgumentNullException.ThrowIfNull(network);
        if (selectedNode is null) {
            return NodeDetails.Empty;
        }

        SemanticNode? node = network.Nodes.FirstOrDefault(
            (SemanticNode item) => item.Id == selectedNode.Id);
        if (node is null) {
            return NodeDetails.Empty;
        }

        // сеть уже прошла валидацию, поэтому ID в словарях однозначны
        Dictionary<string, SemanticNode> nodes = network.Nodes.ToDictionary(
            (SemanticNode item) => item.Id, StringComparer.Ordinal);
        Dictionary<string, RelationDefinition> relations = network.Relations.ToDictionary(
            (RelationDefinition item) => item.Id, StringComparer.Ordinal);
        Dictionary<string, KnowledgeSource> sources = network.Sources.ToDictionary(
            (KnowledgeSource item) => item.Id, StringComparer.Ordinal);

        string kind = node.Kind switch {
            "class" => "Класс",
            "instance" => "Экземпляр",
            "literal" => "Значение",
            _ => node.Kind
        };

        List<string> outgoing = new List<string>();
        List<string> incoming = new List<string>();
        foreach (SemanticEdge edge in network.Edges) {
            if (edge.Source != node.Id && edge.Target != node.Id) {
                continue;
            }

            string fact = $"{edge.Id}: {nodes[edge.Source].Label} [{edge.Source}] — " +
                $"{relations[edge.Relation].Label} → {nodes[edge.Target].Label} [{edge.Target}]";
            if (edge.Source == node.Id) {
                outgoing.Add(fact);
            }

            if (edge.Target == node.Id) {
                incoming.Add(fact);
            }
        }

        string relationsText = "Исходящие связи" + Environment.NewLine +
            (outgoing.Count == 0 ? "Нет исходящих связей." : string.Join(Environment.NewLine, outgoing)) +
            Environment.NewLine + Environment.NewLine + "Входящие связи" + Environment.NewLine +
            (incoming.Count == 0 ? "Нет входящих связей." : string.Join(Environment.NewLine, incoming));

        List<string> provenance = new List<string>();
        foreach (string sourceId in node.SourceRefs) {
            KnowledgeSource source = sources[sourceId];
            provenance.Add($"{source.Id}: {source.Title}" +
                (string.IsNullOrWhiteSpace(source.Description)
                    ? string.Empty
                    : Environment.NewLine + source.Description));
        }

        if (!string.IsNullOrWhiteSpace(node.Assumption)) {
            provenance.Add("Модельное допущение: " + node.Assumption);
        }

        string sourcesText = provenance.Count == 0
            ? "Источники и допущения для этого узла в данных не указаны."
            : string.Join(Environment.NewLine + Environment.NewLine, provenance);

        return new NodeDetails(node.Label, node.Id, kind, node.Description, relationsText, sourcesText);
    }
}
