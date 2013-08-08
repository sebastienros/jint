/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-4.js
 * @description Function.prototype.bind - The [[Enumerable]] attribute of 'arguments' property in  'F' is false
 */


function testcase() {

        var canEnumerable = false;
        var hasProperty = false;
        function foo() { }
        var obj = foo.bind({});
        hasProperty = obj.hasOwnProperty("arguments");
        for (var prop in obj) {
            if (prop === "arguments") {
                canEnumerable = true;
            }
        }
        return hasProperty && !canEnumerable;
    }
runTestCase(testcase);
