/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-9-s.js
 * @description Strict Mode - Use Strict Directive Prologue is ''Use strict';' in which the first character is uppercase
 * @noStrict
 */


function testcase() {
        "Use strict";
        var public = 1;
        return public === 1;
    }
runTestCase(testcase);
