namespace PostyFox_Common
{
    public class Templating
    {
        public void GetTemplate()
        {
            // Get a template from the store

            //_configTable.CreateTableIfNotExists("PostingTemplates");

            //var postingTemplateTable = _configTable.GetTableClient("PostingTemplates");
            //var usersPostingTemplates = postingTemplateTable.Query<PostingTemplateTableEntity>(s => s.PartitionKey == userId);
        }

        public void GeneratePostFromTemplate()
        {
            // Generate a post from a template
        }
    }
}
