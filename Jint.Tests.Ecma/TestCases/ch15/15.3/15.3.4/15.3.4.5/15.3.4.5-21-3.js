/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-3.js
 * @description Function.prototype.bind - [[Set]] attribute of 'arguments' property in  'F' is thrower
 */


function testcase() {

        function foo() { }
        var obj = foo.bind({});
        try {
            obj.arguments = 12;
            return false;
        } catch (ex) {
            return (ex instanceof TypeError);
        }
    }
runTestCase(testcase);
