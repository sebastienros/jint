/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-32-s.js
 * @description Strict Mode - Function code of built-in Function constructor contains Use Strict Directive which appears at the end of the block
 * @noStrict
 */


function testcase() {
        var funObj = new Function("a", "eval('public = 1;'); anotherVariable = 2; 'use strict';");
        funObj();
        return public === 1 && anotherVariable === 2;
    }
runTestCase(testcase);
