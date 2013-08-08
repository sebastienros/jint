/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-10.js
 * @description catch introduces scope - name lookup finds function parameter
 */


function testcase() {
  function f(o) {

    function innerf(o, x) {
      try {
        throw o;
      }
      catch (e) {
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
