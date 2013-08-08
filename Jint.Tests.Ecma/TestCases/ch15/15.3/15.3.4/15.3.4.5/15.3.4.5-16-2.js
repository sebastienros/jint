/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-16-2.js
 * @description Function.prototype.bind - The [[Extensible]] attribute of internal property in F set as true
 */


function testcase() {

        function foo() { }
        var obj = foo.bind({});
        obj.property = 12;
        return obj.hasOwnProperty("property");
    }
runTestCase(testcase);
