/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-1.js
 * @description Function.prototype.bind - 'arguments' is defined as one property of 'F'
 */


function testcase() {

        function foo() { }
        var obj = foo.bind({});
        return obj.hasOwnProperty("arguments");
    }
runTestCase(testcase);
