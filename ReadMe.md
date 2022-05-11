## Pottery Website in AWS ##

This is an attempt to create a serverless, event driven website for a pottery website.  In other words, a set of pots, with pages and category pages, for sale.

Should include a static website, a way of creating a static website from uploaded images and text, author tools for the owner to upload text and images, and a way of triggering website creation.

So far, ImageThumbnailLambda listens for s3 events from a bucket, then copies those items to a target bucket, as well as creating thumbnails of any images uploaded.  So far, we assume these are JPEG.

Next is an API Gateway to listen for calls to write to an S3 bucket (files and images) and a Gateway to a Dynamo DB database to hold a small amout of information about what has been uploaded.

Do we need a lambda for guid creation?

I guess a lambda will listen for events, which will trigger the website 'publish' button.  Perhaps use a queue to trigger an async lambda to do this.

A static website holds the data for the website.

A static website holds the tools to upload text and images and create entries, set entries live and to remove entries that have been sold.

We'll need some kind of authentication for the tools.

I'm not planning on any kind of basket or money taking at the moment.  That can be a future thing.

The domain is thegatehousepottery.com - already aquired.
