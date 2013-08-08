/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.0/13.0-3.js
 * @description 13.0 - property names in function definition is not allowed, add a new property into object
 */


function testcase() {
        var obj = {};
        try {
            eval("function obj.tt() {};");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
