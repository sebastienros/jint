/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-2.js
 * @description Function.prototype.bind - [[Get]] attribute of 'arguments' property in  'F' is thrower
 */


function testcase() {

        function foo() { }
        var obj = foo.bind({});
        try {
            return obj.arguments && false;
        } catch (ex) {
            return (ex instanceof TypeError);
        }
    }
runTestCase(testcase);
