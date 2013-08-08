/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-12-s.js
 * @description Strict Mode - Eval code is strict eval code with a Use Strict Directive in the middle of the block
 * @noStrict
 */


function testcase() {
        eval("var public = 1; 'use strict'; var anotherVariableNotReserveWord = 2;");
        return public === 1 && anotherVariableNotReserveWord === 2;
    }
runTestCase(testcase);
