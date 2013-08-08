/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10.1/12.10.1-5-s.js
 * @description with statement allowed in nested Function even if its container Function is strict)
 * @onlyStrict
 */


function testcase() {
  
    Function("\'use strict\'; var f1 = Function( \"var o = {}; with (o) {};\")");
    return true;
  
 }
runTestCase(testcase);
