/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-0-10.js
 * @description with introduces scope - name lookup finds function parameter
 */


function testcase() {
  function f(o) {

    function innerf(o, x) {
      with (o) {
        return x;
      }
    }

    return innerf(o, 42);
  }
  
  if (f({}) === 42) {
    return true;
  }
 }
runTestCase(testcase);
