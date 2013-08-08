/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.0/13.0-4.js
 * @description 13.0 - multiple names in one function declaration is not allowed, add a new property into a property which is a object
 */


function testcase() {
        var obj = {};
        obj.tt = { len: 10 };
        try {
            eval("function obj.tt.ss() {};");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
