// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Catching objects with try/catch/finally statement
 *
 * @path ch12/12.14/S12.14_A18_T2.js
 * @description Catching null
 */

// CHECK#1
try{
  throw null;
}
catch(e){
  if (e!==null) $ERROR('#1: Exception ===null. Actual: '+e);
}

