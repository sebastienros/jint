/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-11.js
 * @description catch introduces scope - name lookup finds inner variable
 */


function testcase() {
  function f(o) {

    function innerf(o) {
      var x = 42;

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
