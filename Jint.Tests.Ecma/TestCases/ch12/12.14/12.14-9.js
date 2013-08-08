/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-9.js
 * @description catch introduces scope - name lookup finds outer variable
 */


function testcase() {
  function f(o) {
    var x = 42;

    function innerf(o) {
      try {
        throw o;
      }
      catch (e) {
        return x;
      }
    }

    return innerf(o);
  }
  
  if (f({}) === 42) {
    return true;
  }
 }
runTestCase(testcase);
