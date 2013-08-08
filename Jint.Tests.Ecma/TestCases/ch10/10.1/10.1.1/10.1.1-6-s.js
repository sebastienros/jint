/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-6-s.js
 * @description Strict Mode - Use Strict Directive Prologue is ''use strict';' which appears in the middle of the block
 * @noStrict
 */


function testcase() {
        var interface = 2;
        "use strict";
        var public = 1;
        return public === 1 && interface === 2;
    }
runTestCase(testcase);
