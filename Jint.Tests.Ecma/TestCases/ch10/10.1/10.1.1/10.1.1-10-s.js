/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-10-s.js
 * @description Strict Mode - Use Strict Directive Prologue is ''USE STRICT';' in which all characters are uppercase
 * @noStrict
 */


function testcase() {
        "USE STRICT";
        var public = 1;
        return public === 1;
    }
runTestCase(testcase);
