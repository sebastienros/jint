// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * <LF> between chunks of one string not allowed
 *
 * @path ch08/8.4/S8.4_A7.1.js
 * @description Insert <LF> between chunks of one string
 * @negative
 */

eval("var x = asdf\u000Aghjk");

