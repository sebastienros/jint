/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-31-s.js
 * @description Strict Mode - Function code of built-in Function constructor contains Use Strict Directive which appears in the middle of the block
 * @noStrict
 */


function testcase() {
        var funObj = new Function("a", "eval('public = 1;'); 'use strict'; anotherVariable = 2;");
        funObj();
        return public === 1 && anotherVariable === 2;
    }
runTestCase(testcase);
