/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-15-3.js
 * @description Function.prototype.bind - The [[Writable]] attribute of length property in F set as false
 */


function testcase() {

        var canWritable = false;
        var hasProperty = false;
        function foo() { }
        var obj = foo.bind({});
        hasProperty = obj.hasOwnProperty("length");
        obj.length = 100;
        canWritable = (obj.length === 100);
        return hasProperty && !canWritable;
    }
runTestCase(testcase);
